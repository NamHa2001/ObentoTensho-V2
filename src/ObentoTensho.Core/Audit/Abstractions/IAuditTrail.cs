using ObentoTensho.Core.Audit.Domain;

namespace ObentoTensho.Core.Audit.Abstractions;

// BR-STF-12: chỉ thêm vào unit of work — được lưu cùng transaction với thay đổi dữ liệu
public interface IAuditTrail
{
    void Record(AuditEntry entry);
}
