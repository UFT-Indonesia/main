using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using NodaTime;

namespace Erp.UseCases.Attendance.Holidays.Common;

internal sealed class HolidayOnDateSpec : SingleResultSpecification<Holiday>
{
    public HolidayOnDateSpec(LocalDate date)
    {
        Query.Where(holiday => holiday.Date == date);
    }
}

internal sealed class HolidaysInRangeSpec : Specification<Holiday>
{
    public HolidaysInRangeSpec(LocalDate from, LocalDate to)
    {
        Query.Where(holiday => holiday.Date >= from && holiday.Date <= to)
            .OrderBy(holiday => holiday.Date);
        Query.AsNoTracking();
    }
}

/// <summary>
/// Every request whose charge can still move: Pending or Approved, range covering the date,
/// for anyone. Denied and Cancelled requests spend nothing, so their count is left as filed.
/// </summary>
internal sealed class LiveLeaveCoveringDateSpec : Specification<LeaveRequest>
{
    public LiveLeaveCoveringDateSpec(LocalDate date)
    {
        Query.Where(request =>
            (request.Status == LeaveRequestStatus.Pending || request.Status == LeaveRequestStatus.Approved)
            && request.StartDate <= date
            && request.EndDate >= date);
    }
}
