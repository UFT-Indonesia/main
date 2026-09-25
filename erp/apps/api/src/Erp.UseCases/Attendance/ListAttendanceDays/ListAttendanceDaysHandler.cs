using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common.Filtering;
using NodaTime;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// The attendance calendar for a period. No paging: the period bounds the size, and the client
/// expands a date without another request.
/// </summary>
public static class ListAttendanceDaysHandler
{
    public static async Task<Result<ListAttendanceDaysResult>> Handle(
        ListAttendanceDaysQuery query,
        IReadRepository<AttendanceDay> attendanceDays,
        IReadRepository<Employee> employees,
        AttendanceDayPolicy policy,
        IClock clock,
        CancellationToken ct)
    {
        if (!AttendancePeriod.TryValidate(query.From, query.To, out var from, out var to, out var invalid))
        {
            return new Result<ListAttendanceDaysResult>.Error(invalid.Code, invalid.Message);
        }

        if (!FilterApplier.TryCompile(
                AttendanceCalendarFilterFields.Fields, query.Filters, query.Caller, out var filters, out var failure))
        {
            return new Result<ListAttendanceDaysResult>.Error(failure.Code, failure.Message);
        }

        var dates = await AttendanceCalendar.BuildAsync(
            from, to, filters, query.Caller, attendanceDays, employees, policy, clock, ct);

        return new Result<ListAttendanceDaysResult>.Success(new ListAttendanceDaysResult { Dates = dates });
    }
}
