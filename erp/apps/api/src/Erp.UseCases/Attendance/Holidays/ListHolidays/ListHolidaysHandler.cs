using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using NodaTime;

namespace Erp.UseCases.Attendance.Holidays.ListHolidays;

public static class ListHolidaysHandler
{
    /// <summary>
    /// Wide enough for the leave form's last-year-to-next-year window, narrow enough that a typo
    /// can't ask for a century.
    /// </summary>
    public const int MaxRangeDays = 1200;

    public static async Task<Result<IReadOnlyList<HolidayResult>>> Handle(
        ListHolidaysQuery query,
        IReadRepository<Holiday> holidays,
        CancellationToken ct)
    {
        var from = LocalDate.FromDateOnly(query.From);
        var to = LocalDate.FromDateOnly(query.To);

        if (from > to)
        {
            return new Result<IReadOnlyList<HolidayResult>>.Error(
                "holiday.date_range", "From must be on or before to.");
        }

        if (Period.Between(from, to, PeriodUnits.Days).Days >= MaxRangeDays)
        {
            return new Result<IReadOnlyList<HolidayResult>>.Error(
                "holiday.range_too_long", $"Holiday range cannot exceed {MaxRangeDays} days.");
        }

        var found = await holidays.ListAsync(new HolidaysInRangeSpec(from, to), ct);
        return new Result<IReadOnlyList<HolidayResult>>.Success(found.Select(HolidayResult.From).ToList());
    }
}
