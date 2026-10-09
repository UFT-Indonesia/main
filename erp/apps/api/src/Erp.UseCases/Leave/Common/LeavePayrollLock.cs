using NodaTime;

namespace Erp.UseCases.Leave.Common;

/// <summary>
/// Leave with a workday in a month the Owner has closed on the Potongan Cuti page is frozen (GSS03
/// decision 3): only the Owner may edit it, or cancel it once approved, as a correction after close with
/// a reason (follow-up Q8). Everyone else is refused.
/// </summary>
internal static class LeavePayrollLock
{
    internal const string Code = "leave.payroll_closed";

    internal const string ReasonCode = "leave.correction_reason";

    internal const string ReasonMessage =
        "This leave is in a closed payroll month. Changing it is a correction after close and needs a reason.";

    internal static string OwnerOnlyMessage(LocalDate month) =>
        $"Payroll for {month:MMMM yyyy} is closed. Only the Owner can correct this leave.";
}
