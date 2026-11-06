using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ObentoTensho.Api.Http;

// CorrelationId của audit/log là TraceId W3C (16 byte) của yêu cầu, viết dạng Guid
public static class CorrelationIds
{
    public const string Header = "X-Correlation-Id";

    public static Guid? FromActivity(Activity? activity)
    {
        if (activity is null || activity.TraceId == default)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[16];
        activity.TraceId.CopyTo(bytes);
        return new Guid(bytes, bigEndian: true);
    }

    // Dự phòng khi không có Activity: băm định danh yêu cầu để vẫn ổn định trong một yêu cầu
    public static Guid FromTraceIdentifier(string? traceIdentifier) =>
        string.IsNullOrEmpty(traceIdentifier)
            ? Guid.Empty
            : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(traceIdentifier)).AsSpan(0, 16));
}
