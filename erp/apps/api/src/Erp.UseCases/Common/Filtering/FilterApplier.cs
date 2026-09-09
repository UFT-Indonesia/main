using System.Linq.Expressions;
using System.Text.Json;
using Ardalis.Specification;
using Erp.SharedKernel.Domain.Results;

namespace Erp.UseCases.Common.Filtering;

/// <summary>
/// Validates filter rows and appends them to a spec. Rows always AND together, so this is a
/// plain loop of <c>Where</c> calls rather than any expression composition — OR within a field
/// is expressed by the "is any of" operator instead.
/// </summary>
public readonly record struct FilterFailure(string Code, string Message);

public static class FilterApplier
{
    /// <summary>
    /// A hostile caller can otherwise ask for hundreds of predicates in one query string. Well
    /// past anything the UI can produce.
    /// </summary>
    public const int MaxRows = 20;

    /// <summary>
    /// Parses the raw `filter=` query parameter. Returns an empty set for a missing or blank
    /// value so "no filter" needs no special casing at the call sites.
    /// </summary>
    public static Result<IReadOnlyList<FilterRow>> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Result<IReadOnlyList<FilterRow>>.Success([]);
        }

        FilterRow[]? rows;
        try
        {
            rows = JsonSerializer.Deserialize<FilterRow[]>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return Failed("The filter parameter is not valid JSON.", FilterErrors.Malformed);
        }

        if (rows is null || Array.Exists(rows, row => row is null))
        {
            return Failed("The filter parameter must be an array of filter rows.", FilterErrors.Malformed);
        }

        if (rows.Length > MaxRows)
        {
            return Failed($"A filter may hold at most {MaxRows} rows.", FilterErrors.TooManyRows);
        }

        return new Result<IReadOnlyList<FilterRow>>.Success(rows);
    }

    /// <summary>
    /// Validates every row and turns it into a predicate. Compiling up front, rather than inside
    /// a spec constructor, is what lets a bad field or a forbidden one be reported at all — a
    /// specification has nowhere to return failure to. It also means the list spec and the count
    /// spec are handed the identical predicates, so a filtered page and its total cannot disagree.
    /// </summary>
    public static bool TryCompile<T>(
        FilterFieldMap<T> fields,
        IReadOnlyList<FilterRow> rows,
        Caller caller,
        out IReadOnlyList<Expression<Func<T, bool>>> predicates,
        out FilterFailure failure)
    {
        var compiled = new List<Expression<Func<T, bool>>>(rows.Count);
        predicates = compiled;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Field) || !fields.TryGet(row.Field, out var field))
            {
                failure = new FilterFailure(FilterErrors.UnknownField, $"Unknown filter field '{row.Field}'.");
                return false;
            }

            // Standing is checked before the value is even read: a caller who may not filter on
            // a field learns nothing, not even whether their value was well-formed.
            if (!field.Visible(caller))
            {
                failure = new FilterFailure(FilterErrors.FieldForbidden, $"You may not filter by '{row.Field}'.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.Op) || !field.AllowedOps.Contains(row.Op))
            {
                failure = new FilterFailure(
                    FilterErrors.InvalidOperator,
                    $"Operator '{row.Op}' is not valid for '{row.Field}'. Allowed: {string.Join(", ", field.AllowedOps)}.");
                return false;
            }

            try
            {
                compiled.Add(field.Build(row.Op, row.Value));
            }
            catch (FilterValueException ex)
            {
                failure = new FilterFailure(ex.Code, ex.Message);
                return false;
            }
        }

        failure = default;
        return true;
    }

    /// <summary>
    /// Appends the compiled predicates to a spec. Call this <em>after</em> any caller-scoping the
    /// spec applies: filters narrow what a caller may already see, they never widen it.
    /// </summary>
    public static void ApplyTo<T>(
        ISpecificationBuilder<T> query,
        IReadOnlyList<Expression<Func<T, bool>>> predicates)
    {
        foreach (var predicate in predicates)
        {
            query.Where(predicate);
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static Result<IReadOnlyList<FilterRow>> Failed(string message, string code)
        => new Result<IReadOnlyList<FilterRow>>.Error(code, message);

}
