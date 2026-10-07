using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.UseCases.Payroll.Common;
using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// Leave with a workday in a month the Owner has closed on the Potongan Cuti page cannot be edited or
/// cancelled once approved (GSS03 decision 3): that month's figures are frozen and paid out.
/// </summary>
internal static class LeavePayrollLock
{
    internal const string Code = "leave.payroll_closed";

    internal const string Message = "This leave has days in a month whose payroll is already closed.";

    internal static async Task<bool> TouchesClosedMonthAsync(
        LocalDate start, LocalDate end, AttendanceDayPolicy policy,
        IReadRepositoryBase<LeaveDeductionMonth> months, CancellationToken ct)
    {
        var closed = await LeaveDeductionEngine.ClosedMonthsAsync(months, ct);
        return closed.Count > 0
            && LeaveRequest.Workdays(start, end, policy).Any(date => closed.Contains(LeaveDeductionMonth.MonthOf(date)));
    }
}
