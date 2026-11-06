using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ObentoTensho.Infrastructure.Persistence.Conversions;

public sealed class NullableUtcDateTimeConverter()
    : ValueConverter<DateTime?, DateTime?>(
        value => value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
