namespace Erp.UseCases.Attendance.UpdateAttendancePolicy;

public sealed record UpdateAttendancePolicyCommand(
    string ShiftStart,
    string ShiftEnd,
    int ClockInGraceMinutes,
    int ClockOutGraceMinutes,
    string TimeZoneId,
    Guid ChangedByUserId);
