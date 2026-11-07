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

    public Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);
}
