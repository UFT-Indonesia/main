namespace Erp.Core.Aggregates.Attendance;

public enum AttendanceDayStatus
{
    Complete = 0,
    Incomplete = 1,

    /// <summary>
    /// Approved leave covers the day and no punch contradicts it. The only status that is
    /// not derived from punches — see <see cref="AttendanceDay.CreateForLeave"/>.
    /// </summary>
    OnLeave = 2,
}
