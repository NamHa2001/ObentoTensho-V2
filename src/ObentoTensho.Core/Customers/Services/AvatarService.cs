using System.Globalization;
using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;

namespace ObentoTensho.Core.Customers.Services;

// POST /account/avatar (docs/srs/10 §5): ảnh đi qua cùng pipeline với ảnh nội dung (kiểm định dạng thật, bỏ EXIF, sinh WebP).
// Chỉ tài khoản đã xác thực email, giới hạn số lần đổi mỗi ngày để không biến tài khoản khách thành kho chứa ảnh. Ảnh cũ không còn ai dùng
// sẽ bị việc dọn ảnh định kỳ xóa (BR-CNT-32)
public sealed class AvatarService(
    ICustomerRepository customers,
    ImageService images,
    IActionThrottle throttle,
    IAuditTrail audit,
    IUnitOfWork unitOfWork)
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(1);

    public async Task<Result<ImageDto>> SetAsync(Guid userId, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var key = $"avatar:{userId:N}";
        if (throttle.Count(key) >= MediaRules.AvatarUploadsPerDay)
        {
            return Result.Failure<ImageDto>(ErrorCodes.RateLimited);
        }

        // Nạp tài khoản trước để lần lưu của pipeline ảnh ghi luôn mã ảnh mới vào tài khoản
        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { IsDeleted: false } user)
        {
            return Result.Failure<ImageDto>(ErrorCodes.NotFound);
        }

        if (!user.IsEmailVerified)
        {
            return Result.Failure<ImageDto>(ErrorCodes.EmailNotVerified);
        }

        throttle.Increment(key, Window);
        var previous = user.AvatarImageId;
        var uploaded = await images.UploadAvatarAsync(content, userId, cancellationToken);
        if (!uploaded.IsSuccess)
        {
            return uploaded;
        }

        user.AvatarImageId = uploaded.Value.Id;
        audit.Record(new AuditEntry("customers.avatar.changed", "user", userId.ToString("D", CultureInfo.InvariantCulture), Before: new { avatarImageId = previous }, After: new { avatarImageId = user.AvatarImageId }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return uploaded;
    }

    public async Task<Result> RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { IsDeleted: false } user)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (user.AvatarImageId is { } previous)
        {
            user.AvatarImageId = null;
            audit.Record(new AuditEntry("customers.avatar.removed", "user", userId.ToString("D", CultureInfo.InvariantCulture), Before: new { avatarImageId = previous }));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
