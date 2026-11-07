using FluentValidation;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Customers.Services;

// BR-CUS-07, BR-CNT-25: yêu cầu có lý do, mỗi người một lần/ngày, cảnh báo SuperAdmin; tệp do job tạo, chỉ người yêu cầu tải được
public sealed class DataExportService(
    IDataExportRepository exports,
    IStaffRepository staff,
    IAdminEmailOutbox emails,
    ISecureTokenService secureTokens,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IEnumerable<IDataExportSource> sources,
    IValidator<DataExportRequest> validator)
{
    private const int ListLimit = 20;
    private const string Entity = "DataExport";

    public async Task<Result<DataExportDto>> RequestAsync(DataExportKind kind, DataExportRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<DataExportDto>();
        }

        if (await exports.CountSinceAsync(staffId, kind, clock.ToUtc(clock.TodayLocal, TimeOnly.MinValue), cancellationToken) >= 1)
        {
            return Result.Failure<DataExportDto>(ErrorCodes.RateLimited);
        }

        var name = (await staff.FindByIdAsync(staffId, cancellationToken))?.FullName ?? staffId.ToString();
        var superAdmins = await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken);
        var export = new DataExport { Kind = kind, RequestedByStaffId = staffId, Reason = request.Reason.Trim(), CreatedAtUtc = clock.UtcNow };
        return await unitOfWork.InTransactionAsync(async ct =>
        {
            exports.Add(export);
            audit.Record(new AuditEntry($"customers.{Entity}.requested", Entity, export.Id.ToString(), After: new { export.Kind, export.Reason }));
            foreach (var email in superAdmins)
            {
                emails.EnqueueAlert(email, $"{name} yêu cầu xuất dữ liệu ({kind}). Lý do: {export.Reason}", $"data-export:{export.Id:N}");
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(new DataExportDto(export.Id, export.Kind, export.Status, export.RowCount, export.CreatedAtUtc, export.ExpiresAtUtc));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DataExportDto>> ListMineAsync(Guid staffId, CancellationToken cancellationToken) =>
        (await exports.ListForStaffAsync(staffId, ListLimit, cancellationToken))
            .Select(e => new DataExportDto(e.Id, e.Kind, e.Status, e.RowCount, e.CreatedAtUtc, e.ExpiresAtUtc))
            .ToArray();

    public async Task<Result<DataExportFile>> DownloadAsync(DataExportDownloadRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.Token)
            || await exports.FindByTokenHashAsync(secureTokens.Hash(request.Token), cancellationToken) is not { Status: DataExportStatus.Ready, Content: { } content } export
            || export.RequestedByStaffId != staffId
            || export.ExpiresAtUtc <= clock.UtcNow)
        {
            return Result.Failure<DataExportFile>(ErrorCodes.TokenInvalid);
        }

        audit.Record(new AuditEntry($"customers.{Entity}.downloaded", Entity, export.Id.ToString(), After: new { export.Kind, export.RowCount }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var prefix = sources.Single(s => s.Kind == export.Kind).FilePrefix;
        return Result.Success(new DataExportFile(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}-{clock.ToLocal(export.CreatedAtUtc):yyyy-MM-dd}.xlsx"), content));
    }
}
