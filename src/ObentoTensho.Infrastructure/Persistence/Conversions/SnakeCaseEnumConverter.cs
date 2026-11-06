using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ObentoTensho.Infrastructure.Persistence.Conversions;

public sealed class SnakeCaseEnumConverter<TEnum>()
    : ValueConverter<TEnum, string>(value => SnakeCaseEnum.ToSnakeCase(value), text => SnakeCaseEnum.Parse<TEnum>(text))
    where TEnum : struct, Enum;
