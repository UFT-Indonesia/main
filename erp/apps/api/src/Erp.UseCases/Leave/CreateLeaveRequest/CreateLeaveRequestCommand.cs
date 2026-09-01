using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.CreateLeaveRequest;

/// <summary>
/// <paramref name="Attachment"/> arrives already stored: the endpoint streams the upload to
/// disk before dispatching, so the handler deals in a value object rather than a file. Required
/// for Sick, rejected on every other type — see <see cref="LeaveRequest.Create"/>.
/// <paramref name="HalfDay"/>/<paramref name="HalfDayPeriod"/> are Annual's own toggle;
/// <paramref name="StartHour"/>/<paramref name="EndHour"/> are Izin's — see the same validation.
/// </summary>
public sealed record CreateLeaveRequestCommand(
    Guid EmployeeId,
    string Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    LeaveAttachment? Attachment,
    bool HalfDay,
    HalfDayPeriod? HalfDayPeriod,
    int? StartHour,
    int? EndHour,
    Caller Caller);
