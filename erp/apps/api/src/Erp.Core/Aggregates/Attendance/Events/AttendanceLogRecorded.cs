using Erp.SharedKernel.Domain;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance.Events;

public sealed record AttendanceLogRecorded(
    Guid LogId,
    Guid EmployeeId,
    Instant OccurredAtUtc,
    AttendanceSource Source,
    PunchType PunchType) : DomainEvent;
