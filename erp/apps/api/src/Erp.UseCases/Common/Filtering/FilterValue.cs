using System.Text.Json;
using NodaTime;
using NodaTime.Text;

namespace Erp.UseCases.Common.Filtering;

/// <summary>
/// Reads the operator-dependent <see cref="JsonElement"/> payload of a filter row. Every failure
/// is a <see cref="FilterValueException"/> so a malformed value becomes a 400 rather than an
/// unhandled cast deep inside a spec.
/// </summary>
internal static class FilterValue
{
    private static readonly LocalDatePattern DatePattern = LocalDatePattern.Iso;

    public static string Text(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw Invalid("a string");
        }

        var text = value.GetString()!.Trim();
        return text.Length == 0 ? throw Invalid("a non-empty string") : text;
    }

    public static LocalDate Date(JsonElement value)
    {
        var parsed = DatePattern.Parse(Text(value));
        return parsed.Success ? parsed.Value : throw Invalid("a date as yyyy-MM-dd");
    }

    public static (LocalDate From, LocalDate To) DateRange(JsonElement value)
    {
        var (a, b) = Pair(value);
        var from = Date(a);
        var to = Date(b);
        return to < from ? throw Invalid("a range whose end is not before its start") : (from, to);
    }

    public static decimal Number(JsonElement value)
        => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)
            ? d
            : throw Invalid("a number");

    public static (decimal From, decimal To) NumberRange(JsonElement value)
    {
        var (a, b) = Pair(value);
        var from = Number(a);
        var to = Number(b);
        return to < from ? throw Invalid("a range whose end is not below its start") : (from, to);
    }

    public static bool Bool(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw Invalid("true or false"),
    };

    /// <summary>
    /// The array behind "is any of". Empty is rejected rather than silently matching nothing,
    /// which would look identical to a filter that simply found no rows.
    /// </summary>
    public static IReadOnlyList<string> TextSet(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("an array");
        }

        var items = value.EnumerateArray().Select(Text).Distinct(StringComparer.Ordinal).ToList();
        return items.Count == 0 ? throw Invalid("a non-empty array") : items;
    }

    public static IReadOnlyList<TEnum> EnumSet<TEnum>(JsonElement value) where TEnum : struct, Enum
        => TextSet(value)
            .Select(item => Enum.TryParse<TEnum>(item, ignoreCase: true, out var parsed)
                ? parsed
                : throw Invalid($"one of {string.Join(", ", Enum.GetNames<TEnum>())}"))
            .ToList();

    public static IReadOnlyList<Guid> GuidSet(JsonElement value)
        => TextSet(value)
            .Select(item => Guid.TryParse(item, out var parsed) ? parsed : throw Invalid("a GUID"))
            .ToList();

    private static (JsonElement First, JsonElement Second) Pair(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2)
        {
            throw Invalid("an array of exactly two values");
        }

        return (value[0], value[1]);
    }

    private static FilterValueException Invalid(string expected)
        => new(FilterErrors.InvalidValue, $"Filter value must be {expected}.");
}
