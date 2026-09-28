using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShipMate.Infrastructure.Persistence.Conversions;

/// <summary>
/// Stores an enum as its snake_case name (ReadyForLock -> "ready_for_lock"), derived from the enum itself
/// so no per-value mapping has to be written by hand.
/// </summary>
public class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> EnumToDb =
        Enum.GetValues<TEnum>().ToDictionary(value => value, value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    private static readonly Dictionary<string, TEnum> DbToEnum =
        EnumToDb.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static int MaxLength { get; } = EnumToDb.Values.Max(name => name.Length);

    public SnakeCaseEnumConverter()
        : base(value => ToDb(value), name => FromDb(name))
    {
    }

    public static string ToDb(TEnum value) => EnumToDb[value];

    public static TEnum FromDb(string name) => DbToEnum[name];
}
