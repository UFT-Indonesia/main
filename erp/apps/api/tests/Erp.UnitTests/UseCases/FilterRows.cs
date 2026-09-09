using System.Text.Json;
using Erp.UseCases.Common.Filtering;

namespace Erp.UnitTests.UseCases;

/// <summary>Builds filter rows the way the query string would, so tests exercise the real parser.</summary>
internal static class FilterRows
{
    public static FilterRow Row(string field, string op, string valueJson = "null")
        => new(field, op, JsonDocument.Parse(valueJson).RootElement.Clone());

    public static IReadOnlyList<FilterRow> Rows(params FilterRow[] rows) => rows;

    public static IReadOnlyList<FilterRow> None => [];
}
