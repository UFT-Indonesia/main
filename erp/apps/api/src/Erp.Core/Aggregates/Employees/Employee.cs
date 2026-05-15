using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Employees;

public sealed class Employee : AggregateRoot<EmployeeId>
{
    // EF Core constructor.
    private Employee() { }

    private Employee(
        EmployeeId id,
        string fullName,
        Nik nik,
        Npwp? npwp,
        Money monthlyWage,
        LocalDate effectiveSalaryFrom,
        EmployeeRole role,
        EmployeeId? parentId)
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

    public EmployeeId? ParentId { get; private set; }

    public LocalDate? TerminationDate { get; private set; }

    public static Employee Create(
        string fullName,
        Nik nik,
        Money monthlyWage,
        LocalDate effectiveSalaryFrom,
        EmployeeRole role,
        EmployeeId? parentId = null,
        Npwp? npwp = null,
        EmployeeId? id = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("employee.full_name", "Full name is required.");
        }

        if (monthlyWage.Amount <= 0m)
        {
            throw new DomainException("employee.wage", "Monthly wage must be positive.");
        }

        var employeeId = id ?? EmployeeId.New();
        if (parentId.HasValue && parentId.Value == EmployeeId.Empty)
        {
            throw new DomainException("employee.parent_empty", "Parent ID cannot be empty.");
        }

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
            employee.Id.Value,
            employee.FullName,
            nik.Value,
            npwp?.Value,
            role,
            parentId?.Value,
            monthlyWage,
            effectiveSalaryFrom));
        return employee;
    }

    public void UpdateBasicInfo(string fullName, Npwp? npwp)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("employee.full_name", "Full name is required.");
        }

        var trimmed = fullName.Trim();
        var oldFullName = FullName;
        var oldNpwp = Npwp;

        if (string.Equals(oldFullName, trimmed, StringComparison.Ordinal)
            && Equals(oldNpwp, npwp))
        {
            return;
        }

        FullName = trimmed;
        Npwp = npwp;
        RaiseDomainEvent(new EmployeeBasicInfoChanged(
            Id.Value,
            oldFullName,
            FullName,
            oldNpwp?.Value,
            Npwp?.Value));
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
        RaiseDomainEvent(new EmployeeSalaryChanged(Id.Value, oldWage, oldEffective, newWage, effectiveFrom));
    }

    public void AssignParent(EmployeeId? newParentId)
    {
        EnsureActive();
        if (newParentId.HasValue && newParentId.Value == EmployeeId.Empty)
        {
            throw new DomainException("employee.parent_empty", "Parent ID cannot be empty.");
        }

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
        RaiseDomainEvent(new EmployeeParentChanged(Id.Value, old?.Value, newParentId?.Value));
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
        RaiseDomainEvent(new EmployeeRoleChanged(Id.Value, old, newRole));
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
        RaiseDomainEvent(new EmployeeTerminated(Id.Value, terminationDate));
    }

    private void EnsureActive()
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new DomainException("employee.terminated", "Cannot modify a terminated employee.");
        }
    }
}
