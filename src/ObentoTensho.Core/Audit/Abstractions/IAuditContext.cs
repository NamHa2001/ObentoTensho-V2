using ObentoTensho.Core.Audit.Domain;

namespace ObentoTensho.Core.Audit.Abstractions;

// Ai đang gọi, từ IP nào (IP thật sau ForwardedHeaders — SEC-02), thuộc yêu cầu nào (CorrelationId)
public interface IAuditContext
{
    AuditActor Actor { get; }

    string? IpAddress { get; }

    Guid CorrelationId { get; }
}
