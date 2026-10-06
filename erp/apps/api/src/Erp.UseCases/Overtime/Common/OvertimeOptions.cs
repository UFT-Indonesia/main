using Erp.Core.Aggregates.Overtime;

namespace Erp.UseCases.Overtime.Common;

/// <summary>
/// The pay tiers, from <c>appsettings.json</c> / env only — engineer-only, redeploy to change
/// (GSS08 decision 4). Frozen onto each assignment at period close, so a redeploy never
/// rewrites a closed period.
/// </summary>
public sealed class OvertimeOptions
{
    public const string SectionName = "Overtime";

    public List<OvertimeTier> Tiers { get; set; } = [];
}
