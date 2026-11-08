using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Common;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Jobs;

// Gọi khi đang giữ khóa sp_getapplock của việc đó nên hai instance không ghi chồng
internal sealed class JobRunStore(AppDbContext db) : IJobRunStore
{
    public async Task<DateOnly?> LastRunDateAsync(string jobName, CancellationToken cancellationToken) =>
        await db.JobRuns.AsNoTracking().Where(r => r.Name == jobName).Select(r => (DateOnly?)r.LastRunLocalDate).SingleOrDefaultAsync(cancellationToken);

    public async Task RecordAsync(string jobName, DateOnly localDate, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var row = await db.JobRuns.SingleOrDefaultAsync(r => r.Name == jobName, cancellationToken);
        if (row is null)
        {
            db.JobRuns.Add(new JobRun { Name = jobName, LastRunLocalDate = localDate, LastRunAtUtc = nowUtc });
        }
        else
        {
            row.LastRunLocalDate = localDate;
            row.LastRunAtUtc = nowUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
