using Erp.Core.Aggregates.Attendance;
using NodaTime;

namespace Erp.UnitTests;

/// <summary>
/// The standard 09:00–18:00 shift every leave test prices half-days and hourly Izin against,
/// unless a test specifically needs a different shift. Net working hours = 8 (9 minus the
/// 1-hour lunch LeaveRequest assumes at 12:00–13:00).
/// </summary>
internal static class TestPolicies
{
    internal static readonly AttendanceDayPolicy Standard = new(
        new LocalTime(9, 0),
        new LocalTime(18, 0),
        ClockInGraceMinutes: 15,
        ClockOutGraceMinutes: 15,
        MaxIzinHours: 4,
        DateTimeZoneProviders.Tzdb["Asia/Jakarta"]);
}
