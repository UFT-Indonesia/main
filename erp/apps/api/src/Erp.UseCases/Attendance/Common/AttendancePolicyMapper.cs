using Erp.Core.Aggregates.Attendance;
using NodaTime.Text;

namespace Erp.UseCases.Attendance.Common;

public static class AttendancePolicyMapper
{
    private static readonly LocalTimePattern TimePattern =
        LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    public static AttendancePolicyResult ToResult(AttendancePolicy policy) => new()
    {
        ShiftStart = TimePattern.Format(policy.ShiftStart),
        ShiftEnd = TimePattern.Format(policy.ShiftEnd),
        ClockInGraceMinutes = policy.ClockInGraceMinutes,
        ClockOutGraceMinutes = policy.ClockOutGraceMinutes,
        TimeZoneId = policy.TimeZoneId,
        UpdatedByUserId = policy.UpdatedByUserId,
        UpdatedAtUtc = policy.UpdatedAtUtc.ToDateTimeOffset(),
    };

    public static bool TryParseTime(string value, out NodaTime.LocalTime localTime)
    {
        var result = TimePattern.Parse(value);
        if (result.Success)
        {
            localTime = result.Value;
            return true;
        }

        localTime = default;
        return false;
    }
}
