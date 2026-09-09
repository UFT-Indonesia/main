using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Leave.Common;

namespace Erp.UseCases.Leave.ListLeaveRequests;

/// <summary>
/// What the Cuti filter builder may ask about. The split mirrors the projection: everything the
/// leave calendar already shows every colleague is filterable by everyone, while the fields
/// LeaveRequestResult nulls out behind canReadDetails are Owner-only to filter.
/// </summary>
public static class LeaveRequestFilterFields
{
    public static readonly FilterFieldMap<LeaveRequest> Fields = new FilterFieldMap<LeaveRequest>()
        .Relation("employeeId", request => request.EmployeeId, id => new EmployeeId(id))
        .Text("employeeName", request => request.Employee!.FullName)
        .Enum("status", request => request.Status)
        .Date("startDate", request => request.StartDate)
        .Date("endDate", request => request.EndDate)
        .Number("workdayCount", request => request.WorkdayCount)
        .Timestamp("requestedAt", request => request.RequestedAtUtc)
        .Text("decidedByName", request => request.DecidedByName, nullable: true)
        .Timestamp("decidedAt", request => request.DecidedAtUtc)

        // Redacted per row by LeaveRequestResult, so filtering on them is Owner-only.
        .Enum("type", request => request.Type, visible: LeaveRules.CanFilterDetails)
        .Text("reason", request => request.Reason, visible: LeaveRules.CanFilterDetails)
        .Text("decisionNote", request => request.DecisionNote, nullable: true, visible: LeaveRules.CanFilterDetails)
        .Bool("halfDay", request => request.HalfDay, visible: LeaveRules.CanFilterDetails);
}
