using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Customers.Abstractions;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public async Task<(IReadOnlyList<User> Items, int Total)> ListAsync(CustomerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var source = db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted
                && (query.Tier == null || u.MemberTier == query.Tier)
                && (query.IsActive == null || u.IsActive == query.IsActive)
                && (query.FromUtc == null || u.CreatedAtUtc >= query.FromUtc)
                && (query.ToUtc == null || u.CreatedAtUtc < query.ToUtc)
                && (search == null || u.FullName.Contains(search) || u.Email.Contains(search) || (u.PhoneNumber != null && u.PhoneNumber.Contains(search))));
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(u => u.CreatedAtUtc).ThenBy(u => u.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<User>> ListForExportAsync(int limit, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().Where(u => !u.IsDeleted).OrderBy(u => u.CreatedAtUtc).Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerReservationDto>> RecentReservationsAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await db.Reservations.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservedAtUtc)
            .Take(limit)
            .Select(r => new CustomerReservationDto(r.Id, r.ReservationCode, r.ReservedDate, r.ReservedTime, r.Status))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerOrderDto>> RecentOrdersAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(limit)
            .Select(o => new CustomerOrderDto(o.Id, o.OrderCode, o.TotalAmount, o.Status, o.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerVoucherDto>> VouchersAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await db.Vouchers.AsNoTracking()
            .Where(v => v.OwnerUserId == userId)
            .OrderByDescending(v => v.Id)
            .Take(limit)
            .Select(v => new CustomerVoucherDto(v.Id, v.Code, v.Source, v.IsActive, v.EndUtc))
            .ToListAsync(cancellationToken);

    public async Task AnonymizeRelatedAsync(Guid userId, string email, string anonymousEmail, string anonymousName, CancellationToken cancellationToken)
    {
        await db.Orders.Where(o => o.UserId == userId).ExecuteUpdateAsync(
            s => s.SetProperty(o => o.CustomerName, anonymousName)
                .SetProperty(o => o.CustomerPhone, string.Empty)
                .SetProperty(o => o.CustomerEmail, anonymousEmail)
                .SetProperty(o => o.Note, (string?)null),
            cancellationToken);
        await db.Reservations.Where(r => r.UserId == userId).ExecuteUpdateAsync(
            s => s.SetProperty(r => r.GuestName, anonymousName)
                .SetProperty(r => r.GuestPhone, string.Empty)
                .SetProperty(r => r.GuestEmail, anonymousEmail)
                .SetProperty(r => r.SpecialRequest, (string?)null),
            cancellationToken);
        await db.ContactMessages.Where(m => m.Email == email).ExecuteUpdateAsync(
            s => s.SetProperty(m => m.FullName, anonymousName)
                .SetProperty(m => m.Email, anonymousEmail)
                .SetProperty(m => m.PhoneNumber, (string?)null)
                .SetProperty(m => m.IpAddress, (string?)null),
            cancellationToken);
        await db.NewsletterSubscribers.Where(s => s.Email == email).ExecuteUpdateAsync(
            s => s.SetProperty(x => x.Email, anonymousEmail)
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.ConfirmTokenHash, (string?)null)
                .SetProperty(x => x.ConfirmExpiryUtc, (DateTime?)null),
            cancellationToken);
        await db.ConsentRecords.Where(c => c.Email == email || c.UserId == userId).ExecuteUpdateAsync(
            s => s.SetProperty(c => c.Email, anonymousEmail).SetProperty(c => c.Ip, (string?)null),
            cancellationToken);
    }

    public Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);
}
