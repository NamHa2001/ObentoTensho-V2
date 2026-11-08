using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class StaffRepository(AppDbContext db, IMemoryCache cache) : IStaffRepository
{
    // SEC-50: như UserRepository — đệm 5 giây cho kiểm tra security stamp mỗi request
    private static readonly TimeSpan SessionStateCacheDuration = TimeSpan.FromSeconds(5);

    public Task<Staff?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.StaffMembers.AsNoTracking().SingleOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);

    public Task<Staff?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.StaffMembers.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Staff?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.StaffMembers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Staff?> FindByInviteTokenHashForUpdateAsync(string inviteTokenHash, CancellationToken cancellationToken) =>
        db.StaffMembers.SingleOrDefaultAsync(s => s.InviteTokenHash == inviteTokenHash, cancellationToken);

    public Task<Staff?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.StaffMembers.SingleOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);

    public Task<Staff?> FindByResetTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.StaffMembers.SingleOrDefaultAsync(s => s.ResetPasswordTokenHash == tokenHash, cancellationToken);

    public Task<bool> AnyWithRoleAsync(StaffRole role, CancellationToken cancellationToken) =>
        db.StaffMembers.AnyAsync(s => s.Role == role, cancellationToken);

    public async Task<IReadOnlyList<string>> ListActiveEmailsAsync(IReadOnlyCollection<StaffRole> roles, CancellationToken cancellationToken) =>
        await db.StaffMembers.AsNoTracking()
            .Where(s => s.IsActive && roles.Contains(s.Role))
            .Select(s => s.Email)
            .OrderBy(e => e)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Staff> Items, int Total)> ListAsync(StaffQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var source = db.StaffMembers.AsNoTracking()
            .Where(s => (query.Role == null || s.Role == query.Role)
                && (query.IsActive == null || s.IsActive == query.IsActive)
                && (search == null || s.FullName.Contains(search) || s.Email.Contains(search)));
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderBy(s => s.FullName).ThenBy(s => s.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountActiveSuperAdminsForUpdateAsync(CancellationToken cancellationToken) =>
        db.StaffMembers.FromSql($"SELECT * FROM [Staff] WITH (UPDLOCK, HOLDLOCK)")
            .CountAsync(s => s.Role == StaffRole.SuperAdmin && s.IsActive, cancellationToken);

    public async Task<UserSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken, bool freshRead = false)
    {
        var key = $"auth:staff-session:{id:N}";
        if (!freshRead && cache.TryGetValue(key, out UserSessionState? cached))
        {
            return cached;
        }

        var state = await db.StaffMembers.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new UserSessionState(s.IsActive, s.SecurityStamp))
            .SingleOrDefaultAsync(cancellationToken);

        cache.Set(key, state, SessionStateCacheDuration);
        return state;
    }

    public void Add(Staff staff) => db.StaffMembers.Add(staff);

    public Task RegisterFailedLoginAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken)
    {
        const int threshold = AuthPolicy.MaxFailedLogins - 1;
        return db.StaffMembers.Where(s => s.Id == id).ExecuteUpdateAsync(
            u => u
                .SetProperty(s => s.LockoutEndUtc, s => s.FailedLoginCount >= threshold ? lockoutEndUtc : s.LockoutEndUtc)
                .SetProperty(s => s.FailedLoginCount, s => s.FailedLoginCount >= threshold ? 0 : s.FailedLoginCount + 1),
            cancellationToken);
    }

    public Task LockUntilAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken) =>
        db.StaffMembers.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.LockoutEndUtc, lockoutEndUtc), cancellationToken);

    public Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken) =>
        db.StaffMembers.Where(s => s.Id == id && (s.FailedLoginCount > 0 || s.LockoutEndUtc != null)).ExecuteUpdateAsync(
            u => u.SetProperty(s => s.FailedLoginCount, 0).SetProperty(s => s.LockoutEndUtc, (DateTime?)null),
            cancellationToken);

    public Task ReplacePasswordHashAsync(Guid id, string currentHash, string newHash, CancellationToken cancellationToken) =>
        db.StaffMembers.Where(s => s.Id == id && s.PasswordHash == currentHash).ExecuteUpdateAsync(
            u => u.SetProperty(s => s.PasswordHash, newHash),
            cancellationToken);

    public Task RecordLoginAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.StaffMembers.Where(s => s.Id == id).ExecuteUpdateAsync(
            u => u.SetProperty(s => s.LastLoginAtUtc, nowUtc),
            cancellationToken);

    public async Task<bool> TryConsumeTotpStepAsync(Guid id, long timeStep, CancellationToken cancellationToken) =>
        await db.StaffMembers
            .Where(s => s.Id == id && (s.TotpLastUsedStep == null || s.TotpLastUsedStep < timeStep))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.TotpLastUsedStep, timeStep), cancellationToken) == 1;

    public async Task ReplaceRecoveryCodesAsync(Guid staffId, IEnumerable<string> codeHashes, CancellationToken cancellationToken)
    {
        await db.StaffRecoveryCodes.Where(c => c.StaffId == staffId).ExecuteDeleteAsync(cancellationToken);
        db.StaffRecoveryCodes.AddRange(codeHashes.Select(hash => new StaffRecoveryCode { StaffId = staffId, CodeHash = hash }));
    }

    public async Task<bool> TryUseRecoveryCodeAsync(Guid staffId, string codeHash, DateTime nowUtc, CancellationToken cancellationToken) =>
        await db.StaffRecoveryCodes
            .Where(c => c.StaffId == staffId && c.CodeHash == codeHash && c.UsedAtUtc == null)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.UsedAtUtc, nowUtc), cancellationToken) == 1;
}
