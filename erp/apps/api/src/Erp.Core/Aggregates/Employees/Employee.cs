using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using NodaTime;

namespace Erp.Core.Aggregates.Employees;

public sealed class Employee : AggregateRoot
{
    // EF Core constructor.
    private Employee() { }

    private Employee(
        Guid id,
        string fullName,
        Nik nik,
        Npwp? npwp,
        Money monthlyWage,
        LocalDate effectiveSalaryFrom,
        EmployeeRole role,
        Guid? parentId)
        : base(id)
    {
        FullName = fullName;
        Nik = nik;
        Npwp = npwp;
        MonthlyWage = monthlyWage;
        EffectiveSalaryFrom = effectiveSalaryFrom;
        Role = role;
        ParentId = parentId;
        Status = EmployeeStatus.Active;
    }

    public string FullName { get; private set; } = string.Empty;

    public Nik Nik { get; private set; } = default!;

    public Npwp? Npwp { get; private set; }

    public Money MonthlyWage { get; private set; } = default!;

    public LocalDate EffectiveSalaryFrom { get; private set; }

    public EmployeeRole Role { get; private set; }

    public EmployeeStatus Status { get; private set; }

    public Guid? ParentId { get; private set; }

    public LocalDate? TerminationDate { get; private set; }

    public static Employee Create(
        string fullName,
        Nik nik,
        Money monthlyWage,
        LocalDate effectiveSalaryFrom,
        EmployeeRole role,
        Guid? parentId = null,
        Npwp? npwp = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("employee.full_name", "Full name is required.");
        }

        if (monthlyWage.Amount <= 0m)
        {
            throw new DomainException("employee.wage", "Monthly wage must be positive.");
        }

        var employeeId = id ?? Guid.NewGuid();
        if (parentId.HasValue && parentId.Value == employeeId)
        {
            throw new DomainException("employee.parent_self", "Employee cannot be its own parent.");
        }

        if (role == EmployeeRole.Owner && parentId.HasValue)
        {
            throw new DomainException(
                "employee.owner_no_parent",
                "Owner cannot have a parent.");
        }

        if (role != EmployeeRole.Owner && !parentId.HasValue)
        {
            throw new DomainException(
                "employee.parent_required",
                "Non-owner employee must have a parent.");
        }

        var employee = new Employee(
            employeeId,
            fullName.Trim(),
            nik,
            npwp,
            monthlyWage,
            effectiveSalaryFrom,
            role,
            parentId);

        employee.RaiseDomainEvent(new EmployeeCreated(
            employee.Id,
            employee.FullName,
            nik.Value,
            npwp?.Value,
            role,
            parentId,
            monthlyWage,
            effectiveSalaryFrom));
        return employee;
    }

    public void ChangeSalary(Money newWage, LocalDate effectiveFrom)
    {
        EnsureActive();
        if (newWage.Amount <= 0m)
        {
            throw new DomainException("employee.wage", "Monthly wage must be positive.");
        }

        if (effectiveFrom < EffectiveSalaryFrom)
        {
            throw new DomainException(
                "employee.salary_backdated",
                "Effective date cannot be earlier than current effective date.");
        }

        var oldWage = MonthlyWage;
        var oldEffective = EffectiveSalaryFrom;
        MonthlyWage = newWage;
        EffectiveSalaryFrom = effectiveFrom;
        RaiseDomainEvent(new EmployeeSalaryChanged(Id, oldWage, oldEffective, newWage, effectiveFrom));
    }

    public void AssignParent(Guid? newParentId)
    {
        EnsureActive();
        if (newParentId.HasValue && newParentId.Value == Id)
        {
            throw new DomainException("employee.parent_self", "Employee cannot be its own parent.");
        }

        if (Role == EmployeeRole.Owner && newParentId.HasValue)
        {
            throw new DomainException(
                "employee.owner_no_parent",
                "Owner cannot have a parent.");
        }

        if (Role != EmployeeRole.Owner && !newParentId.HasValue)
        {
            throw new DomainException(
                "employee.parent_required",
                "Non-owner employee must have a parent.");
        }

        if (ParentId == newParentId)
        {
            return;
        }

        var old = ParentId;
        ParentId = newParentId;
        RaiseDomainEvent(new EmployeeParentChanged(Id, old, newParentId));
    }

    public void ChangeRole(EmployeeRole newRole)
    {
        EnsureActive();
        if (Role == newRole)
        {
            return;
        }

        if (newRole == EmployeeRole.Owner && ParentId.HasValue)
        {
            throw new DomainException(
                "employee.owner_no_parent",
                "Owner cannot have a parent.");
        }

        if (newRole != EmployeeRole.Owner && !ParentId.HasValue)
        {
            throw new DomainException(
                "employee.parent_required",
                "Non-owner employee must have a parent.");
        }

        var old = Role;
        Role = newRole;
        RaiseDomainEvent(new EmployeeRoleChanged(Id, old, newRole));
    }

    public void Terminate(LocalDate terminationDate)
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new DomainException(
                "employee.already_terminated",
                "Employee already terminated.");
        }

        if (terminationDate < EffectiveSalaryFrom)
        {
            throw new DomainException(
                "employee.terminate_invalid_date",
                "Termination date cannot precede salary effective date.");
        }

        Status = EmployeeStatus.Terminated;
        TerminationDate = terminationDate;
        RaiseDomainEvent(new EmployeeTerminated(Id, terminationDate));
    }

    private void EnsureActive()
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new DomainException("employee.terminated", "Cannot modify a terminated employee.");
        }
    }
}
