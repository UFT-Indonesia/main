using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;

namespace Erp.UseCases.Leave.GetLeaveBalance;

/// <summary>Null <paramref name="Year"/> means the current year.</summary>
public sealed record GetLeaveBalanceQuery(Guid EmployeeId, int? Year, Caller Caller);

public sealed class LeaveBalanceResult
{
    public Guid EmployeeId { get; init; }
    public string EmployeeFullName { get; init; } = default!;
    public int Year { get; init; }
    public bool OnProbation { get; init; }
    public DateOnly? ProbationEndsOn { get; init; }
    public IReadOnlyList<LeaveQuotaResult> Quotas { get; init; } = [];
}
