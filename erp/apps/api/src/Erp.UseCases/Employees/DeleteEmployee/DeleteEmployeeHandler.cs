using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using NodaTime;

namespace Erp.UseCases.Employees.DeleteEmployee;

public sealed class DeleteEmployeeHandler
{
    private readonly IRepository<Employee> _employees;
    private readonly IClock _clock;

    public DeleteEmployeeHandler(IRepository<Employee> employees, IClock clock)
    {
        _employees = employees;
        _clock = clock;
    }

    public async Task<Result<EmployeeResult>> Handle(
        DeleteEmployeeCommand command,
        CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(new EmployeeId(command.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        var terminationDate = command.TerminationDate.HasValue
            ? LocalDate.FromDateTime(command.TerminationDate.Value.ToDateTime(TimeOnly.MinValue))
            : _clock.GetCurrentInstant().InUtc().Date;

        try
        {
            employee.Terminate(terminationDate);
        }
        catch (DomainException ex)
        {
            return new Result<EmployeeResult>.Error(ex.Code ?? "employee.validation", ex.Message);
        }

        await _employees.UpdateAsync(employee, ct);
        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
