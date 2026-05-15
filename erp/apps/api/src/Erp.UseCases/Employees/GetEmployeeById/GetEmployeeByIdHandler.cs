using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;

namespace Erp.UseCases.Employees.GetEmployeeById;

public sealed class GetEmployeeByIdHandler
{
    private readonly IReadRepository<Employee> _employees;

    public GetEmployeeByIdHandler(IReadRepository<Employee> employees)
    {
        _employees = employees;
    }

    public async Task<Result<EmployeeResult>> Handle(
        GetEmployeeByIdQuery query,
        CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(new EmployeeId(query.EmployeeId), ct);
        if (employee is null)
        {
            return new Result<EmployeeResult>.NotFound("Employee was not found.");
        }

        return new Result<EmployeeResult>.Success(EmployeeMapper.ToResult(employee));
    }
}
