using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ObentoTensho.Infrastructure.Persistence.Conversions;

// TIME-01: mọi mốc thời gian lưu UTC; đọc lại luôn gắn Kind = Utc.
public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
