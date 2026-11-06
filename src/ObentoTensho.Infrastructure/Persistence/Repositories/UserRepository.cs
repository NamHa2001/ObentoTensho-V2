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

    public async Task<UserSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = $"auth:user-session:{id:N}";
        if (cache.TryGetValue(key, out UserSessionState? cached))
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

    public Task ResetFailedLoginsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(
            s => s.SetProperty(u => u.FailedLoginCount, 0).SetProperty(u => u.LockoutEndUtc, (DateTime?)null),
            cancellationToken);

    public Task ReplacePasswordHashAsync(Guid id, string currentHash, string newHash, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == id && u.PasswordHash == currentHash).ExecuteUpdateAsync(
            s => s.SetProperty(u => u.PasswordHash, newHash),
            cancellationToken);
}
