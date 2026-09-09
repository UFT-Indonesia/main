using System.Linq.Expressions;
using System.Text.Json;

namespace Erp.UseCases.Common.Filtering;

public enum FilterDataType
{
    Text,
    Enum,
    Date,
    Number,
    Bool,
    Relation,
}

/// <summary>Thrown by a field's value parser; <see cref="FilterApplier"/> turns it into a Result error.</summary>
public sealed class FilterValueException : Exception
{
    public FilterValueException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}

/// <summary>
/// One filterable field: the operators it accepts, who may use it, and how to turn an operator
/// plus a raw JSON value into a predicate. Instances are created through the fluent methods on
/// <see cref="FilterFieldMap{T}"/>, never directly.
/// </summary>
public sealed class FilterField<T>
{
    private readonly Func<string, JsonElement, Expression<Func<T, bool>>> _build;

    private readonly Func<string, JsonElement> _sample;

    internal FilterField(
        FilterDataType dataType,
        IReadOnlySet<string> allowedOps,
        Func<Caller, bool>? visible,
        Func<string, JsonElement, Expression<Func<T, bool>>> build,
        Func<string, JsonElement> sample)
    {
        DataType = dataType;
        AllowedOps = allowedOps;
        Visible = visible ?? (static _ => true);
        _build = build;
        _sample = sample;
    }

    public FilterDataType DataType { get; }

    public IReadOnlySet<string> AllowedOps { get; }

    /// <summary>
    /// Who may filter on this field. Checked before the row is applied, because a predicate the
    /// caller may not read is a disclosure oracle even when the response redacts the column:
    /// the row set that comes back still answers the question. Never rely on the UI hiding it.
    /// </summary>
    public Func<Caller, bool> Visible { get; }

    public Expression<Func<T, bool>> Build(string op, JsonElement value) => _build(op, value);

    /// <summary>
    /// A value this field would accept for <paramref name="op"/>. Exists so the exhaustive
    /// translation test can drive every field and operator without hand-written fixtures — which
    /// is what makes it impossible to add a field the test quietly skips. A registry entry that
    /// EF cannot translate compiles fine and only fails when a user picks it, so the test has to
    /// be the thing that cannot be forgotten.
    /// </summary>
    public JsonElement SampleValue(string op) => _sample(op);
}
