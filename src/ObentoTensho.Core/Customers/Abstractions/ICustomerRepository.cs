using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Core.Customers.Abstractions;

// BR-CUS-06: mỗi mục một truy vấn riêng có giới hạn, chỉ các cột cần
public interface ICustomerRepository
{
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(CustomerQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListForExportAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerReservationDto>> RecentReservationsAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerOrderDto>> RecentOrdersAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerVoucherDto>> VouchersAsync(Guid userId, int limit, CancellationToken cancellationToken);

    // Bản được theo dõi; bỏ qua tài khoản đã xóa
    Task<User?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);
}
