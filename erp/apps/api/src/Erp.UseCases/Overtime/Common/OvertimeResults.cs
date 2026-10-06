using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Overtime;
using Erp.Core.Interfaces;
using Erp.UseCases.Common;
using Microsoft.Extensions.Options;

namespace Erp.UseCases.Overtime.Common;

public sealed record OvertimeAssignmentResult(
    Guid Id,
    Guid EmployeeId,
    string EmployeeFullName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool EndsNextDay,
    bool IsDayOff,
    string Status,
    string? DecidedByName,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionNote,
    DateTimeOffset? TapInUtc,
    DateTimeOffset? TapOutUtc,
    int Hours,
    bool Late,
    bool LeftEarly,
    bool Incomplete,
    /// <summary>Null when the caller may not see pay (a Manager).</summary>
    decimal? Amount,
    bool IsFrozen,
    bool CanDecide,
    bool CanManage,
    bool CanFileCorrection);

public sealed record OvertimeCorrectionResult(
    Guid Id,
    Guid AssignmentId,
    Guid EmployeeId,
    string EmployeeFullName,
    DateOnly WorkDate,
    string Kind,
    DateTimeOffset PunchedAtUtc,
    string Reason,
    string AttachmentFileName,
    string Status,
    DateTimeOffset RequestedAtUtc,
    string? DecidedByName,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionNote,
    bool CanDecide);

public sealed record RapelResult(
    Guid Id,
    Guid AssignmentId,
    Guid EmployeeId,
    string EmployeeFullName,
    DateOnly WorkDate,
    TimeOnly ClaimedStart,
    TimeOnly ClaimedEnd,
    int ClaimedHours,
    decimal SuggestedAmount,
    string Note,
    string AttachmentFileName,
    string Status,
    decimal? Amount,
    DateOnly? PayoutPeriodStart,
    DateTimeOffset RequestedAtUtc,
    string? DecidedByName,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionNote);

internal static class OvertimeMapper
{
    /// <summary>Evaluates one assignment against its own punches and maps it for the caller.</summary>
    public static async Task<OvertimeAssignmentResult> DescribeAsync(
        OvertimeAssignment a, Employee subject, Caller caller, IReadRepository<AttendanceLog> logs,
        AttendanceDayPolicy policy, IOptions<OvertimeOptions> options, CancellationToken ct)
    {
        var punches = await OvertimeEvaluator.LoadPunchesAsync([a], logs, policy, ct);
        return ToResult(a, subject, OvertimeEvaluator.Evaluate(a, punches, policy, options.Value.Tiers), caller);
    }

    public static OvertimeAssignmentResult ToResult(
        OvertimeAssignment a, Employee subject, OvertimeEvaluation e, Caller caller)
    {
        var editable = a.IsLive && !a.IsFrozen;
        return new OvertimeAssignmentResult(
            a.Id.Value,
            a.EmployeeId.Value,
            subject.FullName,
            a.Date.ToDateOnly(),
            a.StartTime.ToTimeOnly(),
            a.EndTime.ToTimeOnly(),
            a.EndDate != a.Date,
            a.IsDayOff,
            a.Status.ToString(),
            a.DecidedByName,
            a.DecidedAtUtc?.ToDateTimeOffset(),
            a.DecisionNote,
            e.TapIn?.ToDateTimeOffset(),
            e.TapOut?.ToDateTimeOffset(),
            e.Hours,
            e.Count.Late,
            e.Count.LeftEarly,
            e.Count.Incomplete,
            OvertimeRules.CanSeePay(caller, subject) ? e.Amount : null,
            a.IsFrozen,
            CanDecide: a.Status == OvertimeStatus.Pending && !a.IsFrozen && OvertimeRules.CanDecide(caller),
            CanManage: editable && OvertimeRules.CanAssign(caller, subject),
            CanFileCorrection: a.Status == OvertimeStatus.Approved && !a.IsFrozen && (e.Count.Incomplete || e.Count.LeftEarly)
                && OrgScope.IsSelf(caller, subject));
    }

    public static OvertimeCorrectionResult ToResult(OvertimeCorrectionRequest c, Employee subject, bool canDecide) => new(
        c.Id.Value, c.AssignmentId.Value, c.EmployeeId.Value, subject.FullName, c.WorkDate.ToDateOnly(), c.Kind.ToString(),
        c.PunchedAtUtc.ToDateTimeOffset(), c.Reason, c.Attachment.FileName, c.Status.ToString(), c.RequestedAtUtc.ToDateTimeOffset(),
        c.DecidedByName, c.DecidedAtUtc?.ToDateTimeOffset(), c.DecisionNote, canDecide);

    public static RapelResult ToResult(Rapel r, Employee subject) => new(
        r.Id.Value, r.AssignmentId.Value, r.EmployeeId.Value, subject.FullName, r.WorkDate.ToDateOnly(),
        r.ClaimedStart.ToTimeOnly(), r.ClaimedEnd.ToTimeOnly(), r.ClaimedHours, r.SuggestedAmount, r.Note, r.Attachment.FileName,
        r.Status.ToString(), r.Amount, r.PayoutPeriodStart?.ToDateOnly(), r.RequestedAtUtc.ToDateTimeOffset(),
        r.DecidedByName, r.DecidedAtUtc?.ToDateTimeOffset(), r.DecisionNote);
}
