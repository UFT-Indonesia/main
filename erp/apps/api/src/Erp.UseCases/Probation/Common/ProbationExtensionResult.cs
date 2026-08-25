using Erp.Core.Aggregates.Probation;
using Erp.UseCases.Common;

namespace Erp.UseCases.Probation.Common;

public sealed class ProbationExtensionResult
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateOnly CurrentEndsOn { get; init; }
    public DateOnly ProposedEndsOn { get; init; }
    public string Reason { get; init; } = default!;
    public string Status { get; init; } = default!;
    public Guid RequestedByUserId { get; init; }
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }

    /// <summary>Server-computed so the UI never has to know the reporting line.</summary>
    public bool CanDecide { get; init; }
    public bool CanCancel { get; init; }

    public static ProbationExtensionResult From(
        ProbationExtensionRequest request,
        Caller caller,
        string? employeeFullName = null)
    {
        var pending = request.Status == ProbationExtensionStatus.Pending;

        return new ProbationExtensionResult
        {
            Id = request.Id.Value,
            EmployeeId = request.EmployeeId.Value,
            EmployeeFullName = employeeFullName ?? request.Employee?.FullName ?? "—",
            CurrentEndsOn = request.CurrentEndsOn.ToDateOnly(),
            ProposedEndsOn = request.ProposedEndsOn.ToDateOnly(),
            Reason = request.Reason,
            Status = request.Status.ToString(),
            RequestedByUserId = request.RequestedByUserId,
            RequestedAtUtc = request.RequestedAtUtc.ToDateTimeOffset(),
            DecidedByName = request.DecidedByName,
            DecidedAtUtc = request.DecidedAtUtc?.ToDateTimeOffset(),
            DecisionNote = request.DecisionNote,
            CanDecide = pending && ProbationRules.CanDecide(caller),
            CanCancel = pending && ProbationRules.CanCancel(caller, request.RequestedByUserId),
        };
    }
}
