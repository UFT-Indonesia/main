using Erp.Core.Aggregates.Attendance;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Attendance.ListAttendanceDays;

/// <summary>
/// What the Kehadiran filter builder may ask about. Nothing here is redacted, so no field needs
/// a visibility rule — which rows a caller sees is decided by ApplyCallerScope on the spec, and
/// these filters only narrow that further.
/// </summary>
public static class AttendanceDayFilterFields
{
    public static readonly FilterFieldMap<AttendanceDay> Fields = new FilterFieldMap<AttendanceDay>()
        .Text("employeeName", day => day.Employee!.FullName)
        .Relation("employeeId", day => day.EmployeeId, id => new EmployeeId(id))
        .Date("date", day => day.CalendarDate)
        .Enum("status", day => day.Status)
        .Timestamp("tapIn", day => day.TapInUtc)
        .Timestamp("tapOut", day => day.TapOutUtc);
}
