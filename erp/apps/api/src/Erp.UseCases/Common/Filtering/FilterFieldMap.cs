using System.Linq.Expressions;
using System.Text.Json;
using NodaTime;
using static Erp.UseCases.Common.Filtering.FilterExpr;
using static Erp.UseCases.Common.Filtering.FilterExprCombine;
using static Erp.UseCases.Common.Filtering.FilterOps;

namespace Erp.UseCases.Common.Filtering;

/// <summary>
/// The set of fields one resource exposes to the filter builder, declared fluently beside its
/// spec. This is the whitelist: a field key that is not in here has no path to a column, so an
/// unknown or hostile key fails lookup rather than reaching the database.
/// </summary>
public sealed class FilterFieldMap<T>
{
    private readonly Dictionary<string, FilterField<T>> _fields = new(StringComparer.Ordinal);

    public bool TryGet(string key, out FilterField<T> field) => _fields.TryGetValue(key, out field!);

    public IEnumerable<string> Keys => _fields.Keys;

    /// <summary>A real string column: supports the full text operator set.</summary>
    public FilterFieldMap<T> Text(
        string key,
        Expression<Func<T, string?>> selector,
        bool nullable = false,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Text, TextOps(nullable), visible, (op, value) => op switch
        {
            Contains => Compose(selector, Lowered(value, needle => v => v != null && v.ToLower().Contains(needle))),
            NotContains => Compose(selector, Lowered(value, needle => v => v == null || !v.ToLower().Contains(needle))),
            StartsWith => Compose(selector, Lowered(value, needle => v => v != null && v.ToLower().StartsWith(needle))),
            Is => Compose(selector, Lowered(value, needle => v => v != null && v.ToLower() == needle)),
            IsNot => Compose(selector, Lowered(value, needle => v => v == null || v.ToLower() != needle)),
            IsEmpty => Compose(selector, v => v == null || v == ""),
            _ => Compose(selector, v => v != null && v != ""),
        }, Sample("\"sample\""));

    /// <summary>
    /// A value object stored through a whole-property <c>HasConversion</c> — NIK, NPWP. EF maps
    /// the object to a single column and cannot see inside it, so <c>v.Value.Contains(...)</c>
    /// is untranslatable and only equality is offered. <paramref name="parse"/> is the domain
    /// factory, so an ill-formed value is rejected as a 400 instead of matching nothing.
    /// </summary>
    public FilterFieldMap<T> ConvertedText<TValue>(
        string key,
        Expression<Func<T, TValue?>> selector,
        Func<string, TValue> parse,
        string sample,
        bool nullable = false,
        Func<Caller, bool>? visible = null)
        where TValue : class
        => Add(key, FilterDataType.Text, ConvertedTextOps(nullable), visible, (op, value) => op switch
        {
            Is => Compare(selector, ExpressionType.Equal, Parse(value, parse)),
            IsNot => Compare(selector, ExpressionType.NotEqual, Parse(value, parse)),
            IsEmpty => Compare(selector, ExpressionType.Equal, (TValue?)null),
            _ => Compare(selector, ExpressionType.NotEqual, (TValue?)null),
        }, Sample(JsonSerializer.Serialize(sample)));

    public FilterFieldMap<T> Enum<TEnum>(
        string key,
        Expression<Func<T, TEnum>> selector,
        Func<Caller, bool>? visible = null)
        where TEnum : struct, Enum
        => Add(key, FilterDataType.Enum, SetOps, visible, (op, value) =>
        {
            var set = FilterValue.EnumSet<TEnum>(value).ToArray();
            return op == In
                ? Compose(selector, v => set.Contains(v))
                : Compose(selector, v => !set.Contains(v));
        }, SetSample(JsonSerializer.Serialize(System.Enum.GetNames<TEnum>()[0])));

    /// <summary>
    /// A string column drawn from a fixed vocabulary — an event type, a code. Behaves like an
    /// enum to the user (multi-select, "is any of") but is not a CLR enum, so the allowed values
    /// live in the frontend descriptor rather than being derived from a type.
    /// </summary>
    public FilterFieldMap<T> Choice(
        string key,
        Expression<Func<T, string>> selector,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Enum, SetOps, visible, (op, value) =>
        {
            var set = FilterValue.TextSet(value).ToArray();
            return op == In
                ? Compose(selector, v => set.Contains(v))
                : Compose(selector, v => !set.Contains(v));
        }, SetSample("\"sample\""));

    /// <summary>A foreign key held as a strongly-typed id; the wire carries plain GUIDs.</summary>
    public FilterFieldMap<T> Relation<TId>(
        string key,
        Expression<Func<T, TId>> selector,
        Func<Guid, TId> fromGuid,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Relation, SetOps, visible, (op, value) =>
        {
            var set = FilterValue.GuidSet(value).Select(fromGuid).ToArray();
            return op == In
                ? Compose(selector, v => set.Contains(v))
                : Compose(selector, v => !set.Contains(v));
        }, SetSample($"\"{Guid.Empty}\""));

    /// <summary>A calendar date column, compared directly as a <see cref="LocalDate"/>.</summary>
    public FilterFieldMap<T> Date(
        string key,
        Expression<Func<T, LocalDate>> selector,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Date, DateOps(nullable: false), visible, (op, value) =>
            BuildDate(selector, op, value, d => d), DateSample);

    public FilterFieldMap<T> Date(
        string key,
        Expression<Func<T, LocalDate?>> selector,
        bool nullable = true,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Date, DateOps(nullable), visible, (op, value) => op switch
        {
            IsEmpty => Compare(selector, ExpressionType.Equal, (LocalDate?)null),
            IsNotEmpty => Compare(selector, ExpressionType.NotEqual, (LocalDate?)null),
            _ => BuildDate(selector, op, value, d => (LocalDate?)d),
        }, DateSample);

    /// <summary>
    /// An instant column filtered by calendar day. The day is anchored in
    /// <see cref="DisplayZone.Jakarta"/> and the upper bound is exclusive — start of the next
    /// local day — which is exactly what the audit log spec already did by hand. Doing this in
    /// UTC would slice days at 07:00 local and quietly drop or add rows at both ends.
    /// </summary>
    public FilterFieldMap<T> Timestamp(
        string key,
        Expression<Func<T, Instant>> selector,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Date, DateOps(nullable: false), visible, (op, value) =>
            BuildTimestamp(selector, op, value, i => i), DateSample);

    public FilterFieldMap<T> Timestamp(
        string key,
        Expression<Func<T, Instant?>> selector,
        bool nullable = true,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Date, DateOps(nullable), visible, (op, value) => op switch
        {
            IsEmpty => Compare(selector, ExpressionType.Equal, (Instant?)null),
            IsNotEmpty => Compare(selector, ExpressionType.NotEqual, (Instant?)null),
            _ => BuildTimestamp(selector, op, value, i => (Instant?)i),
        }, DateSample);

    public FilterFieldMap<T> Number<TNumber>(
        string key,
        Expression<Func<T, TNumber>> selector,
        Func<Caller, bool>? visible = null)
        where TNumber : struct
        => Add(key, FilterDataType.Number, NumberOps, visible, (op, value) =>
        {
            TNumber Cast(decimal d) => (TNumber)Convert.ChangeType(d, typeof(TNumber));

            if (op == Between)
            {
                var (from, to) = FilterValue.NumberRange(value);
                return And(
                    Compare(selector, ExpressionType.GreaterThanOrEqual, Cast(from)),
                    Compare(selector, ExpressionType.LessThanOrEqual, Cast(to)));
            }

            var number = Cast(FilterValue.Number(value));
            return op switch
            {
                Is => Compare(selector, ExpressionType.Equal, number),
                IsNot => Compare(selector, ExpressionType.NotEqual, number),
                GreaterThan => Compare(selector, ExpressionType.GreaterThan, number),
                _ => Compare(selector, ExpressionType.LessThan, number),
            };
        }, Sample("1", "[1,2]"));

    public FilterFieldMap<T> Bool(
        string key,
        Expression<Func<T, bool>> selector,
        Func<Caller, bool>? visible = null)
        => Add(key, FilterDataType.Bool, BoolOps, visible, (_, value) =>
            Compare(selector, ExpressionType.Equal, FilterValue.Bool(value)), Sample("true"));


    private static Expression<Func<T, bool>> BuildDate<TDate>(
        Expression<Func<T, TDate>> selector,
        string op,
        JsonElement value,
        Func<LocalDate, TDate> lift)
    {
        if (op == Between)
        {
            var (from, to) = FilterValue.DateRange(value);
            return And(
                Compare(selector, ExpressionType.GreaterThanOrEqual, lift(from)),
                Compare(selector, ExpressionType.LessThanOrEqual, lift(to)));
        }

        var date = lift(FilterValue.Date(value));
        return op switch
        {
            Is => Compare(selector, ExpressionType.Equal, date),
            Before => Compare(selector, ExpressionType.LessThan, date),
            _ => Compare(selector, ExpressionType.GreaterThan, date),
        };
    }

    private static Expression<Func<T, bool>> BuildTimestamp<TInstant>(
        Expression<Func<T, TInstant>> selector,
        string op,
        JsonElement value,
        Func<Instant, TInstant> lift)
    {
        static Instant StartOfDay(LocalDate day) => day.AtStartOfDayInZone(DisplayZone.Jakarta).ToInstant();

        if (op == Between)
        {
            var (from, to) = FilterValue.DateRange(value);
            return And(
                Compare(selector, ExpressionType.GreaterThanOrEqual, lift(StartOfDay(from))),
                Compare(selector, ExpressionType.LessThan, lift(StartOfDay(to.PlusDays(1)))));
        }

        var day = FilterValue.Date(value);
        return op switch
        {
            Is => And(
                Compare(selector, ExpressionType.GreaterThanOrEqual, lift(StartOfDay(day))),
                Compare(selector, ExpressionType.LessThan, lift(StartOfDay(day.PlusDays(1))))),
            Before => Compare(selector, ExpressionType.LessThan, lift(StartOfDay(day))),
            _ => Compare(selector, ExpressionType.GreaterThanOrEqual, lift(StartOfDay(day.PlusDays(1)))),
        };
    }

    private static Expression<Func<string?, bool>> Lowered(
        JsonElement value,
        Func<string, Expression<Func<string?, bool>>> build)
        => build(FilterValue.Text(value).ToLowerInvariant());

    private static TValue Parse<TValue>(JsonElement value, Func<string, TValue> parse)
    {
        try
        {
            return parse(FilterValue.Text(value));
        }
        catch (Exception ex) when (ex is not FilterValueException)
        {
            throw new FilterValueException(FilterErrors.InvalidValue, ex.Message);
        }
    }

    private FilterFieldMap<T> Add(
        string key,
        FilterDataType dataType,
        IReadOnlySet<string> ops,
        Func<Caller, bool>? visible,
        Func<string, JsonElement, Expression<Func<T, bool>>> build,
        Func<string, JsonElement> sample)
    {
        _fields.Add(key, new FilterField<T>(dataType, ops, visible, build, sample));
        return this;
    }

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    /// <summary>Blank operators take no value; everything else gets the type's own sample.</summary>
    private static Func<string, JsonElement> Sample(string single, string? range = null)
        => op => op switch
        {
            IsEmpty or IsNotEmpty => Json("null"),
            Between => Json(range ?? single),
            _ => Json(single),
        };

    private static Func<string, JsonElement> SetSample(string member)
        => _ => Json($"[{member}]");

    private static readonly Func<string, JsonElement> DateSample =
        Sample("\"2024-01-01\"", "[\"2024-01-01\",\"2024-12-31\"]");

    private static readonly IReadOnlySet<string> SetOps = new HashSet<string>(StringComparer.Ordinal) { In, NotIn };
    private static readonly IReadOnlySet<string> BoolOps = new HashSet<string>(StringComparer.Ordinal) { Is };
    private static readonly IReadOnlySet<string> NumberOps =
        new HashSet<string>(StringComparer.Ordinal) { Is, IsNot, GreaterThan, LessThan, Between };

    private static IReadOnlySet<string> TextOps(bool nullable)
        => WithBlank(nullable, Contains, NotContains, StartsWith, Is, IsNot);

    private static IReadOnlySet<string> ConvertedTextOps(bool nullable) => WithBlank(nullable, Is, IsNot);

    private static IReadOnlySet<string> DateOps(bool nullable) => WithBlank(nullable, Is, Before, After, Between);

    /// <summary>Blank checks are offered only where a null is actually possible.</summary>
    private static IReadOnlySet<string> WithBlank(bool nullable, params string[] ops)
    {
        var set = new HashSet<string>(ops, StringComparer.Ordinal);
        if (nullable)
        {
            set.Add(IsEmpty);
            set.Add(IsNotEmpty);
        }

        return set;
    }
}
