using Ardalis.Specification;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;

namespace Erp.UseCases.Leave.GetLeaveAttachment;

/// <summary>
/// Hands back the doctor's note on a Sick request, for the people allowed to read that
/// request's details. Gated by <see cref="LeaveRules.CanReadDetails"/> — the same rule as the
/// reason text, because a doctor's note is the same health data in a different container.
/// </summary>
public static class GetLeaveAttachmentHandler
{
    public static async Task<Result<LeaveAttachmentContent>> Handle(
        GetLeaveAttachmentQuery query,
        IReadRepository<LeaveRequest> leaveRequests,
        IReadRepository<Employee> employees,
        ILeaveAttachmentStorage storage,
        CancellationToken ct)
    {
        var request = await leaveRequests.FirstOrDefaultAsync(
            new LeaveRequestWithEmployeeSpec(new LeaveRequestId(query.LeaveRequestId)), ct);

        if (request is null)
        {
            return new Result<LeaveAttachmentContent>.NotFound("Leave request was not found.");
        }

        var subject = request.Employee
            ?? await employees.GetByIdAsync(request.EmployeeId, ct);

        if (subject is null)
        {
            return new Result<LeaveAttachmentContent>.NotFound("Employee was not found.");
        }

        if (!LeaveRules.CanReadDetails(query.Caller, subject))
        {
            return new Result<LeaveAttachmentContent>.Error(
                ResultErrors.Forbidden, "You cannot read this request's attachment.");
        }

        if (request.Attachment is not { } attachment)
        {
            return new Result<LeaveAttachmentContent>.NotFound("This request has no attachment.");
        }

        var content = await storage.OpenAsync(attachment.StorageKey, ct);
        if (content is null)
        {
            // The row says there is a file and the store disagrees. Reported rather than
            // swallowed as a 404 — a missing file behind a live row is a real problem.
            throw new DomainException(
                "leave.attachment_missing",
                "The attachment is recorded on this request but is missing from storage.");
        }

        return new Result<LeaveAttachmentContent>.Success(
            new LeaveAttachmentContent(content, attachment.FileName, attachment.ContentType));
    }
}

/// <summary>One request with its employee, for the permission check.</summary>
internal sealed class LeaveRequestWithEmployeeSpec : SingleResultSpecification<LeaveRequest>
{
    public LeaveRequestWithEmployeeSpec(LeaveRequestId id)
    {
        Query.Where(request => request.Id == id);
        Query.Include(request => request.Employee);
        Query.AsNoTracking();
    }
}
