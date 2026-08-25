using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Probation;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Probation.Common;
using NodaTime;

namespace Erp.UseCases.Probation.CreateProbationExtensionRequest;

/// <summary>
/// A Manager asking for more probation time for one of their own Staff. Only filable while the
/// employee is actually still on probation — once the date has passed they are confirmed, and
/// un-confirming someone retroactively is an edit, not a request.
/// </summary>
public static class CreateProbationExtensionRequestHandler
{
    public static async Task<Result<ProbationExtensionResult>> Handle(
        CreateProbationExtensionRequestCommand command,
        IReadRepository<Employee> employees,
        IRepository<ProbationExtensionRequest> requests,
        IClock clock,
        CancellationToken ct)
    {
        var employeeId = new EmployeeId(command.EmployeeId);
        var employee = await employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
        {
            return new Result<ProbationExtensionResult>.NotFound("Employee was not found.");
        }

        if (!ProbationRules.CanFileFor(command.Caller, employee))
        {
            return new Result<ProbationExtensionResult>.Error(
                ResultErrors.Forbidden,
                "You can only request a probation extension for your own direct staff.");
        }

        if (employee.Status == EmployeeStatus.Terminated)
        {
            return new Result<ProbationExtensionResult>.Error(
                "probation.employee_terminated",
                "Cannot request a probation extension for a terminated employee.");
        }

        var today = DisplayZone.Today(clock);
        if (employee.ProbationEndsOn is not { } currentEndsOn || !employee.IsOnProbation(today))
        {
            return new Result<ProbationExtensionResult>.Error(
                "probation.already_confirmed",
                $"{employee.FullName} is no longer on probation.");
        }

        if (await requests.AnyAsync(new PendingProbationExtensionForEmployeeSpec(employeeId), ct))
        {
            return new Result<ProbationExtensionResult>.Error(
                "probation.pending_exists",
                "This employee already has a pending probation extension request.");
        }

        ProbationExtensionRequest request;
        try
        {
            request = ProbationExtensionRequest.Create(
                employeeId,
                currentEndsOn,
                LocalDate.FromDateOnly(command.ProposedEndsOn),
                command.Reason,
                command.Caller.UserId,
                clock.GetCurrentInstant());
        }
        catch (DomainException ex)
        {
            return new Result<ProbationExtensionResult>.Error(ex.Code ?? "probation.validation", ex.Message);
        }

        await requests.AddAsync(request, ct);

        return new Result<ProbationExtensionResult>.Success(
            ProbationExtensionResult.From(request, command.Caller, employee.FullName));
    }
}
