using Erp.UseCases.Common;

namespace Erp.UseCases.Attendance.ExportAttendanceDays;

public sealed record AttendanceDayKey(Guid EmployeeId, DateOnly Date);

public sealed record ExportAttendanceDaysQuery(IReadOnlyList<AttendanceDayKey> Items, Caller Caller);
