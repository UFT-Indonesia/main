using Erp.UseCases.Attendance.Holidays.Common;

namespace Erp.Web.Endpoints.Attendance.Holidays;

public sealed class ListHolidaysRequest
{
    /// <summary>Inclusive start of the window.</summary>
    public DateOnly From { get; init; }

    /// <summary>Inclusive end of the window.</summary>
    public DateOnly To { get; init; }
}

public sealed class SaveHolidayRequest
{
    /// <summary>From the route.</summary>
    public DateOnly Date { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>"National" (libur nasional) or "Collective" (cuti bersama).</summary>
    public string Kind { get; init; } = string.Empty;
}

public sealed class RemoveHolidayRequest
{
    public DateOnly Date { get; init; }
}

public sealed class HolidayResponse
{
    public DateOnly Date { get; init; }
    public string Name { get; init; } = default!;
    public string Kind { get; init; } = default!;

    public static HolidayResponse From(HolidayResult result) => new()
    {
        Date = result.Date,
        Name = result.Name,
        Kind = result.Kind,
    };
}
