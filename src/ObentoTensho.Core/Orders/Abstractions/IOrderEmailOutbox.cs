using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Core.Orders.Abstractions;

// Thư cho khách về đơn (docs/srs/20 #9, #10, #11, #28), ghi vào hộp thư đi cùng transaction
public interface IOrderEmailOutbox
{
    void EnqueueReceived(Order order, string accessToken);

    // BR-ORD-12: gửi mọi khách có email, không chỉ thành viên
    void EnqueueReady(Order order);

    void EnqueueCancelled(Order order, string reason);

    void EnqueueCancelledByCustomer(Order order);

    void EnqueueRefunded(Order order);
}
