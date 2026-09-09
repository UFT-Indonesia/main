namespace Erp.IntegrationTests;

/// <summary>
/// Builds the `filter=` query parameter the way the web app does. Kept in one place so a change
/// to the wire format breaks compilation here rather than silently returning unfiltered rows —
/// an unknown query parameter is ignored by model binding, so a stale test would still pass
/// while asserting nothing.
/// </summary>
internal static class FilterQuery
{
    public static string Rows(string json) => $"filter={Uri.EscapeDataString(json)}";

    public static string Url(string path, string json) => $"{path}?{Rows(json)}";
}
