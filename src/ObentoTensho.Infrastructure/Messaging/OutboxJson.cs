using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObentoTensho.Infrastructure.Messaging;

// Enum lưu dạng tên để thêm/sắp lại giá trị enum không làm sai tin đang chờ trong hộp thư đi
public static class OutboxJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };
}
