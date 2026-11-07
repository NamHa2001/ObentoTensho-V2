using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Customers.Services;

// BR-CUS-07: mỗi phút tạo một tệp, gửi liên kết cho người yêu cầu; tệp quá 24 giờ bị xóa
public sealed class DataExportJob(
    IDataExportRepository exports,
    IStaffRepository staff,
    IAdminEmailOutbox emails,
    ISecureTokenService secureTokens,
    ITableSpreadsheet spreadsheet,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IEnumerable<IDataExportSource> sources) : IRecurringJob
{
    public string Name => "customers.data-export";

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var expired = await exports.ExpireAsync(clock.UtcNow, cancellationToken);
        if (await exports.FindNextQueuedAsync(cancellationToken) is not { } export)
        {
            return expired;
        }

        var source = sources.Single(s => s.Kind == export.Kind);
        var rows = await source.LoadRowsAsync(cancellationToken);
        var token = secureTokens.Create();
        export.Content = spreadsheet.Write(source.SheetName, source.Columns, rows);
        export.RowCount = rows.Count;
        export.DownloadTokenHash = token.Hash;
        export.ExpiresAtUtc = clock.UtcNow + DataExport.Lifetime;
        export.Status = DataExportStatus.Ready;
        audit.Record(new AuditEntry("customers.DataExport.created", "DataExport", export.Id.ToString(), After: new { export.Kind, export.RowCount }, Actor: AuditActor.System));
        if (await staff.FindByIdAsync(export.RequestedByStaffId, cancellationToken) is { IsActive: true } member)
        {
            emails.EnqueueExportReady(member.Email, export, token.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return expired + 1;
    }
}
