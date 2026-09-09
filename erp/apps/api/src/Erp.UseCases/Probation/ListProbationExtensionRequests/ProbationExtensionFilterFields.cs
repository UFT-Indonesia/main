using Erp.Core.Aggregates.Probation;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;

namespace Erp.UseCases.Probation.ListProbationExtensionRequests;

/// <summary>
/// What the Masa Percobaan filter builder may ask about. Which rows a caller can reach at all is
/// decided by the scope the handler resolves (Owner sees everyone, Manager only their direct
/// Staff); these filters narrow that set and never widen it, so no field needs its own rule.
/// </summary>
public static class ProbationExtensionFilterFields
{
    public static readonly FilterFieldMap<ProbationExtensionRequest> Fields =
        new FilterFieldMap<ProbationExtensionRequest>()
            .Relation("employeeId", request => request.EmployeeId, id => new EmployeeId(id))
            .Text("employeeName", request => request.Employee!.FullName)
            .Enum("status", request => request.Status)
            .Date("currentEndsOn", request => request.CurrentEndsOn)
            .Date("proposedEndsOn", request => request.ProposedEndsOn)
            .Text("reason", request => request.Reason)
            .Timestamp("requestedAt", request => request.RequestedAtUtc)
            .Text("decidedByName", request => request.DecidedByName, nullable: true)
            .Timestamp("decidedAt", request => request.DecidedAtUtc);
}
