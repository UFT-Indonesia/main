using Erp.Core.Aggregates.Attendance;

namespace Erp.UseCases.Attendance.Holidays.Common;

public sealed record HolidayResult
{
    public DateOnly Date { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>"National" (libur nasional) or "Collective" (cuti bersama).</summary>
    public string Kind { get; init; } = string.Empty;

    public static HolidayResult From(Holiday holiday) => new()
    {
        Date = holiday.Date.ToDateOnly(),
        Name = holiday.Name,
        Kind = holiday.Kind.ToString(),
    };
}
