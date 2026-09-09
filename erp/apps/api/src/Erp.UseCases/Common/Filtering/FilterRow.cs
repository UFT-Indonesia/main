using System.Text.Json;

namespace Erp.UseCases.Common.Filtering;

/// <summary>
/// One row of the filter builder, exactly as it arrives on the wire. <see cref="Value"/> stays
/// a raw <see cref="JsonElement"/> because its shape depends on the operator — a string for
/// "contains", a two-element array for "between", nothing at all for "is empty" — and only the
/// field's own <see cref="FilterField{T}"/> knows which to expect.
/// </summary>
public sealed record FilterRow(string Field, string Op, JsonElement Value);

/// <summary>
/// Operator vocabulary shared with the frontend's OPERATORS_BY_TYPE. These strings are part of
/// the API contract: they appear in the `filter=` query param, so renaming one is a breaking
/// change.
/// </summary>
public static class FilterOps
{
    // text
    public const string Contains = "contains";
    public const string NotContains = "ncontains";
    public const string StartsWith = "startswith";
    public const string IsEmpty = "empty";
    public const string IsNotEmpty = "nempty";

    // equality, shared by text, number and bool
    public const string Is = "is";
    public const string IsNot = "isnot";

    // sets, used by enum and relation
    public const string In = "in";
    public const string NotIn = "nin";

    // dates
    public const string Before = "before";
    public const string After = "after";
    public const string Between = "between";

    // numbers
    public const string GreaterThan = "gt";
    public const string LessThan = "lt";
}

/// <summary>Error codes the applier returns; endpoints map forbidden to 403 and the rest to 400.</summary>
public static class FilterErrors
{
    public const string UnknownField = "filter.unknown_field";
    public const string InvalidOperator = "filter.invalid_operator";
    public const string InvalidValue = "filter.invalid_value";
    public const string FieldForbidden = "filter.field_forbidden";
    public const string TooManyRows = "filter.too_many_rows";
    public const string Malformed = "filter.malformed";
}
