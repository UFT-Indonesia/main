namespace Erp.Core.Aggregates.Overtime;

/// <summary>One pay tier: a day with at least <paramref name="MinHours"/> counted hours pays <paramref name="Amount"/>.</summary>
public sealed record OvertimeTier(int MinHours, decimal Amount);
