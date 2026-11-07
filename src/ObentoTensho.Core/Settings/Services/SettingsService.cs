using System.Globalization;
using ObentoTensho.Contracts.Dtos.Settings;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Settings.Services;

// FR-RPT-03: danh mục cố định trong mã, giá trị trong CSDL. Ghi theo lô, audit trước-sau, khóa thanh toán có kiểm soát (BR-RPT-33)
public sealed class SettingsService(
    ISettingsRepository settings,
    SettingsSeeder seeder,
    StaffStepUpService stepUp,
    IImageRepository images,
    IStaffRepository staff,
    IAdminEmailOutbox adminEmails,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const string Entity = "SystemSetting";

    public async Task<IReadOnlyList<SettingDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = (await settings.ListAsync(cancellationToken)).ToDictionary(s => s.Key, StringComparer.Ordinal);
        return SettingsCatalog.All
            .Select(d =>
            {
                var row = rows.GetValueOrDefault(d.Key);
                return new SettingDto(d.Key, row?.Value ?? d.Default, d.Type, d.Min, d.Max, d.Choices, d.Protected, d.Description, row?.UpdatedAtUtc);
            })
            .ToArray();
    }

    public async Task<Result> UpdateAsync(UpdateSettingsRequest request, Guid staffId, StaffRole role, string? stepUpToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = await ValidateAsync(request.Values, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, errors);
        }

        var protectedKeys = request.Values.Keys.Where(k => SettingsCatalog.Find(k) is { Protected: true }).ToArray();
        if (protectedKeys.Length > 0)
        {
            if (!StaffCapabilities.Has(role, StaffCapability.PaymentSettings))
            {
                return Result.Failure(ErrorCodes.Forbidden);
            }

            if (!await stepUp.IsValidAsync(staffId, stepUpToken, cancellationToken))
            {
                return Result.Failure(ErrorCodes.StepUpRequired);
            }
        }

        var superAdmins = protectedKeys.Length > 0 ? await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken) : [];
        var saved = await unitOfWork.TryInTransactionAsync(ct => ApplyAsync(request.Values, staffId, protectedKeys, superAdmins, ct), cancellationToken);
        if (!saved.IsSuccess)
        {
            return Result.Failure(saved.ErrorCode ?? ErrorCodes.InternalError, saved.FieldErrors);
        }

        await seeder.RefreshAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SendTestEmailAsync(Guid staffId, CancellationToken cancellationToken)
    {
        if (await staff.FindByIdAsync(staffId, cancellationToken) is not { } member)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        adminEmails.EnqueueAlert(member.Email, "Thư thử từ trang Cài đặt.", $"test-email:{clock.UtcNow.Ticks}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<bool>> ApplyAsync(
        IReadOnlyDictionary<string, string> values,
        Guid staffId,
        string[] protectedKeys,
        IReadOnlyList<string> superAdmins,
        CancellationToken cancellationToken)
    {
        var rows = (await settings.ListForUpdateAsync(cancellationToken)).ToDictionary(s => s.Key, StringComparer.Ordinal);
        var tierError = CheckTierOrder(rows, values);
        if (tierError is not null)
        {
            return Result.Failure<bool>(ErrorCodes.ValidationFailed, tierError);
        }

        var now = clock.UtcNow;
        var changed = new List<string>();
        foreach (var (key, value) in values)
        {
            var row = rows[key];
            if (string.Equals(row.Value, value, StringComparison.Ordinal))
            {
                continue;
            }

            audit.Record(new AuditEntry($"settings.{Entity}.updated", Entity, key, new { row.Value }, new { Value = value }));
            row.Value = value;
            row.UpdatedByStaffId = staffId;
            row.UpdatedAtUtc = now;
            changed.Add(key);
        }

        var sensitive = changed.Intersect(protectedKeys, StringComparer.Ordinal).ToArray();
        if (sensitive.Length > 0)
        {
            var message = $"Thông tin nhạy cảm vừa được đổi: {string.Join(", ", sensitive)}.";
            foreach (var email in superAdmins)
            {
                adminEmails.EnqueueAlert(email, message, $"settings:{now.Ticks.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var imageIds = new List<(string Key, Guid Id)>();
        foreach (var (key, value) in values)
        {
            if (SettingsCatalog.Find(key) is not { } definition)
            {
                errors[key] = [FieldErrorCodes.NotFound];
            }
            else if (SettingRules.Check(definition, value) is { } error)
            {
                errors[key] = [error];
            }
            else if (definition.Type == SettingType.Image && Guid.TryParse(value, out var id))
            {
                imageIds.Add((key, id));
            }
        }

        // BR-RPT-25: chỉ nhận ảnh đã tải qua pipeline
        var loaded = await images.LoadAsync(imageIds.Select(i => i.Id).ToArray(), cancellationToken);
        foreach (var (key, id) in imageIds.Where(i => !loaded.ContainsKey(i.Id)))
        {
            errors[key] = [FieldErrorCodes.NotFound];
        }

        return errors;
    }

    // BR-LOY-03: ngưỡng hạng tăng dần, xét cả giá trị sắp ghi lẫn giá trị đang có
    private static Dictionary<string, string[]>? CheckTierOrder(Dictionary<string, SystemSetting> rows, IReadOnlyDictionary<string, string> values)
    {
        int Value(string key) => int.Parse(values.TryGetValue(key, out var next) ? next : rows[key].Value, CultureInfo.InvariantCulture);

        return Value(SettingKeys.LoyaltyTierSilver) < Value(SettingKeys.LoyaltyTierGold) && Value(SettingKeys.LoyaltyTierGold) < Value(SettingKeys.LoyaltyTierDiamond)
            ? null
            : new Dictionary<string, string[]>(StringComparer.Ordinal) { [SettingKeys.LoyaltyTierSilver] = [FieldErrorCodes.OutOfRange] };
    }
}
