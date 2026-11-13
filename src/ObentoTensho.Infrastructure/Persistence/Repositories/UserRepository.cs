using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext db, IMemoryCache cache) : IUserRepository
{
    // SEC-50: bộ đệm L1 ngắn để không truy vấn CSDL ở mọi request mà vẫn thu hồi gần như tức thì
    private static readonly TimeSpan SessionStateCacheDuration = TimeSpan.FromSeconds(5);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<UserSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken, bool freshRead = false)
    {
        var key = $"auth:user-session:{id:N}";
        if (!freshRead && cache.TryGetValue(key, out UserSessionState? cached))
        {
            return cached;
        }

        var state = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserSessionState(u.IsActive && !u.IsDeleted, u.SecurityStamp))
            .SingleOrDefaultAsync(cancellationToken);

        cache.Set(key, state, SessionStateCacheDuration);
        return state;
    }

    public Task<User?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByEmailVerifyTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.EmailVerifyTokenHash == tokenHash, cancellationToken);

    public Task<User?> FindByResetTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.ResetPasswordTokenHash == tokenHash, cancellationToken);

    public Task<User?> FindByPendingEmailHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.PendingEmailTokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<User>> ListDueForDeletionAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken) =>
        await db.Users.Where(u => !u.IsDeleted && u.DeletionScheduledAtUtc <= nowUtc).OrderBy(u => u.DeletionScheduledAtUtc).Take(limit).ToListAsync(cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public Task RegisterFailedLoginAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken)
    {
        // Mọi vế phải của UPDATE đọc giá trị cũ nên hai cột đổi nhất quán trong một câu lệnh
        const int threshold = AuthPolicy.MaxFailedLogins - 1;
        return db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(
            s => s
                .SetProperty(u => u.LockoutEndUtc, u => u.FailedLoginCount >= threshold ? lockoutEndUtc : u.LockoutEndUtc)
                .SetProperty(u => u.FailedLoginCount, u => u.FailedLoginCount >= threshold ? 0 : u.FailedLoginCount + 1),
            cancellationToken);
    }

    public Task LockUntilAsync(Guid id, DateTime lockoutEndUtc, CancellationToken cancellationToken) =>
        db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.LockoutEndUtc, lockoutEndUtc), cancellationToken);

    public Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(
            s => s.SetProperty(u => u.FailedLoginCount, 0).SetProperty(u => u.LockoutEndUtc, (DateTime?)null),
            cancellationToken);

    public Task ReplacePasswordHashAsync(Guid id, string currentHash, string newHash, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == id && u.PasswordHash == currentHash).ExecuteUpdateAsync(
            s => s.SetProperty(u => u.PasswordHash, newHash),
            cancellationToken);

    public Task<bool> IsPhoneVerifiedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Id == id && u.IsPhoneVerified && u.IsActive && !u.IsDeleted, cancellationToken);

    public Task<bool> IsEmailVerifiedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Id == id && u.IsEmailVerified && u.IsActive && !u.IsDeleted, cancellationToken);

    public Task<bool> PhoneVerifiedByOtherAsync(string phoneNumber, Guid exceptUserId, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.PhoneNumber == phoneNumber && u.IsPhoneVerified && !u.IsDeleted && u.Id != exceptUserId, cancellationToken);
}
