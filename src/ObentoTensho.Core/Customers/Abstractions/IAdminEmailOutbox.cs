using ObentoTensho.Core.Customers.Domain;

namespace ObentoTensho.Core.Customers.Abstractions;

// docs/srs/20 #29: thư cho quản trị, ghi vào hộp thư đi cùng transaction
public interface IAdminEmailOutbox
{
    // SEC-58; key: cùng khóa chỉ gửi một lần
    void EnqueueAlert(string staffEmail, string message, string key);

    void EnqueueExportReady(string staffEmail, DataExport export, string token);
}
