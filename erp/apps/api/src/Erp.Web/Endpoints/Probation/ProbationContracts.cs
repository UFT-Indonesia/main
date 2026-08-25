using Erp.UseCases.Probation.Common;

namespace Erp.Web.Endpoints.Probation;

public sealed class CreateProbationExtensionRequest
{
    public Guid EmployeeId { get; init; }
    /// <summary>Must be later than the employee's current probation end date.</summary>
    public DateOnly ProposedEndsOn { get; init; }
    public string Reason { get; init; } = default!;
}

public sealed class ListProbationExtensionsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public Guid? EmployeeId { get; init; }
}

public sealed class DecideProbationExtensionRequest
{
    public Guid Id { get; init; }
    public string? Note { get; init; }
}

public sealed class ProbationExtensionResponse
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public DateOnly CurrentEndsOn { get; init; }
    public DateOnly ProposedEndsOn { get; init; }
    public string Reason { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTimeOffset RequestedAtUtc { get; init; }
    public string? DecidedByName { get; init; }
    public DateTimeOffset? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }

    /// <summary>What the calling user may do — drives which controls the UI renders.</summary>
    public bool CanDecide { get; init; }
    public bool CanCancel { get; init; }

    public static ProbationExtensionResponse From(ProbationExtensionResult result) => new()
    {
        Id = result.Id,
        EmployeeId = result.EmployeeId,
        EmployeeFullName = result.EmployeeFullName,
        CurrentEndsOn = result.CurrentEndsOn,
        ProposedEndsOn = result.ProposedEndsOn,
        Reason = result.Reason,
        Status = result.Status,
        RequestedAtUtc = result.RequestedAtUtc,
        DecidedByName = result.DecidedByName,
        DecidedAtUtc = result.DecidedAtUtc,
        DecisionNote = result.DecisionNote,
        CanDecide = result.CanDecide,
        CanCancel = result.CanCancel,
    };
}

public sealed class ListProbationExtensionsResponse
{
    public IReadOnlyList<ProbationExtensionResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
