using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Customers.Services;

public sealed class AccountDeletionJob(AccountDeletionService deletion) : IRecurringJob
{
    public string Name => "customers.account-deletion";

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public Task<int> RunAsync(CancellationToken cancellationToken) => deletion.AnonymizeDueAsync(cancellationToken);
}
