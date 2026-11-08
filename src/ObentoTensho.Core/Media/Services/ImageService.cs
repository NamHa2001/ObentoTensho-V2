using System.Globalization;
using System.Security.Cryptography;
using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Media.Services;

// docs/srs/04 §2, SEC-10: nhận ảnh, lưu gốc, sinh biến thể. Ảnh nhỏ xử lý ngay, ảnh lớn để việc định kỳ xử lý (FR-IMG-08)
public sealed class ImageService(
    IImageRepository images,
    IImageProcessor processor,
    IImageFileStore files,
    ImageMapper mapper,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int JobBatchSize = 5;
    private const string Entity = "image";

    public async Task<Result<ImageDto>> UploadAsync(Stream content, string? altText, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var alt = altText?.Trim() ?? string.Empty;
        if (alt.Length is 0 or > MediaRules.AltTextMaxLength)
        {
            return Result.Failure<ImageDto>(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["altText"] = [alt.Length == 0 ? FieldErrorCodes.Required : FieldErrorCodes.TooLong],
            });
        }

        return await StoreAsync(content, alt, new Uploader(staffId, UserId: null), MediaRules.MaxUploadBytes, cancellationToken);
    }

    // Ảnh đại diện của khách: giới hạn nhỏ hơn và luôn sinh biến thể ngay (không chờ việc định kỳ)
    public Task<Result<ImageDto>> UploadAvatarAsync(Stream content, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        return StoreAsync(content, "Ảnh đại diện", new Uploader(StaffId: null, userId), MediaRules.AvatarMaxBytes, cancellationToken);
    }

    private async Task<Result<ImageDto>> StoreAsync(Stream content, string alt, Uploader uploader, long maxBytes, CancellationToken cancellationToken)
    {
        var bytes = await ReadAllAsync(content, maxBytes, cancellationToken);
        if (bytes is null)
        {
            return Result.Failure<ImageDto>(ErrorCodes.FileTooLarge);
        }

        var probe = processor.Probe(bytes);
        if (!probe.IsSuccess)
        {
            return Result.Failure<ImageDto>(probe.ErrorCode ?? ErrorCodes.FileInvalid);
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (await images.FindByHashAsync(hash, cancellationToken) is { Status: not ImageStatus.Failed } existing)
        {
            return Result.Success(mapper.ToDto(existing));
        }

        var id = Guid.CreateVersion7();
        var image = new StoredImage
        {
            Id = id,
            OriginalPath = await files.SaveOriginalAsync(id, bytes, cancellationToken),
            Width = probe.Value.Width,
            Height = probe.Value.Height,
            AltText = alt,
            ContentHash = hash,
            CreatedByStaffId = uploader.StaffId,
            CreatedByUserId = uploader.UserId,
            CreatedAtUtc = clock.UtcNow,
        };
        images.Add(image);
        audit.Record(new AuditEntry("media.image.uploaded", Entity, id.ToString("D", CultureInfo.InvariantCulture), After: new { image.Width, image.Height, bytes.Length }));
        if ((long)image.Width * image.Height <= MediaRules.SyncMaxPixels)
        {
            await GenerateAsync(image, bytes, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(mapper.ToDto(image));
    }

    public async Task<Result<ImageDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await images.FindAsync(id, cancellationToken) is { } image
            ? Result.Success(mapper.ToDto(image))
            : Result.Failure<ImageDto>(ErrorCodes.NotFound);

    // BR-CNT-30: nội dung chỉ tham chiếu ảnh đã có trong pipeline
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        await images.FindAsync(id, cancellationToken) is { Status: not ImageStatus.Failed };

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var done = 0;
        foreach (var id in await images.ListProcessingAsync(JobBatchSize, cancellationToken))
        {
            if (await images.FindAsync(id, cancellationToken) is not { Status: ImageStatus.Processing } image)
            {
                continue;
            }

            await GenerateAsync(image, await files.ReadOriginalAsync(image.OriginalPath, cancellationToken), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            done++;
        }

        return done;
    }

    // FR-IMG-08: nút "Thử lại" ở thư viện ảnh sau khi đã hết lượt tự thử lại
    public async Task<Result<ImageDto>> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await images.FindAsync(id, cancellationToken) is not { } image)
        {
            return Result.Failure<ImageDto>(ErrorCodes.NotFound);
        }

        if (image.Status != ImageStatus.Failed)
        {
            return Result.Failure<ImageDto>(ErrorCodes.InvalidTransition);
        }

        image.Status = ImageStatus.Processing;
        image.ProcessingAttempts = 0;
        audit.Record(new AuditEntry($"media.{Entity}.retry", Entity, id.ToString("D"), After: new { image.Status }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(mapper.ToDto(image));
    }

    private async Task GenerateAsync(StoredImage image, byte[] original, CancellationToken cancellationToken)
    {
        try
        {
            var processed = processor.Process(original);
            foreach (var variant in processed.Variants)
            {
                await files.SaveVariantAsync(image.Id, image.Version, variant, cancellationToken);
            }

            image.Width = processed.Width;
            image.Height = processed.Height;
            image.BlurHash = processed.BlurHash;
            image.VariantWidths = string.Join(',', processed.Variants.Select(v => v.Width.ToString(CultureInfo.InvariantCulture)));
            image.Status = ImageStatus.Ready;
            image.ProcessingAttempts = 0;
            image.FailedAtUtc = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // Giữ ảnh gốc để sinh lại sau; ảnh Failed không dùng được cho nội dung
            image.Status = ImageStatus.Failed;
            image.ProcessingAttempts++;
            image.FailedAtUtc = clock.UtcNow;
        }
    }

    private static async Task<byte[]?> ReadAllAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private sealed record Uploader(Guid? StaffId, Guid? UserId);
}
