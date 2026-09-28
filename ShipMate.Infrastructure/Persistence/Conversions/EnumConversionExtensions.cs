using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShipMate.Infrastructure.Persistence.Conversions;

public static class EnumConversionExtensions
{
    public static PropertyBuilder<TEnum> HasSnakeCaseConversion<TEnum>(this PropertyBuilder<TEnum> builder)
        where TEnum : struct, Enum =>
        builder
            .HasConversion(new SnakeCaseEnumConverter<TEnum>())
            .HasMaxLength(SnakeCaseEnumConverter<TEnum>.MaxLength);

    public static PropertyBuilder<TEnum?> HasSnakeCaseConversion<TEnum>(this PropertyBuilder<TEnum?> builder)
        where TEnum : struct, Enum =>
        builder
            .HasConversion(new SnakeCaseEnumConverter<TEnum>())
            .HasMaxLength(SnakeCaseEnumConverter<TEnum>.MaxLength);

    // Stored as a native Postgres text[] of snake_case names.
    public static PropertyBuilder<List<TEnum>> HasSnakeCaseArrayConversion<TEnum>(this PropertyBuilder<List<TEnum>> builder)
        where TEnum : struct, Enum =>
        builder
            .HasConversion(
                values => values.Select(value => SnakeCaseEnumConverter<TEnum>.ToDb(value)).ToArray(),
                names => names.Select(name => SnakeCaseEnumConverter<TEnum>.FromDb(name)).ToList(),
                new ValueComparer<List<TEnum>>(
                    (left, right) => left!.SequenceEqual(right!),
                    values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)),
                    values => values.ToList()))
            .HasColumnType("text[]");
}
