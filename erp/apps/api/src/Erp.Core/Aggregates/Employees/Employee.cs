using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Employees;

public sealed class Employee : AggregateRoot<EmployeeId>
{
    /// <summary>Default probation length, counted from the hire date. UU 13/2003 art. 60's cap.</summary>
    public const int ProbationMonths = 3;

    private readonly List<EmployeeLeaveQuota> _leaveQuotas = new();

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
        EmployeeId? parentId,
        LocalDate? hireDate)
        : base(id)
    {
        FullName = fullName;
        HireDate = hireDate;
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

    /// <summary>
    /// First day of employment, and the anchor probation is counted from. Null means "hired
    /// before this field existed": such an employee is never on probation and gets a full
    /// entitlement, rather than being handed a sentinel date nobody chose.
    /// </summary>
    public LocalDate? HireDate { get; private set; }

    /// <summary>
    /// An Owner's deliberate probation end date, overriding the 3-month default. Kept separate
    /// from <see cref="HireDate"/> so a later correction to the hire date never silently moves
    /// a date someone set on purpose.
    /// </summary>
    public LocalDate? ProbationEndsOnOverride { get; private set; }

    /// <summary>
    /// Effective probation end: the Owner's override if there is one, otherwise three months
    /// from the hire date. Null means no probation at all.
    /// </summary>
    public LocalDate? ProbationEndsOn => ProbationEndsOnOverride ?? HireDate?.PlusMonths(ProbationMonths);

    /// <summary>Exclusive of the end date itself — probation is over on the day it ends.</summary>
    public bool IsOnProbation(LocalDate today) => ProbationEndsOn is { } endsOn && today < endsOn;

    public IReadOnlyCollection<EmployeeLeaveQuota> LeaveQuotas => _leaveQuotas.AsReadOnly();

    /// <summary>The override for one leave type, or null when there is no row for it.</summary>
    public int? QuotaOverride(LeaveType type) =>
        _leaveQuotas.FirstOrDefault(quota => quota.Type == type)?.EntitledDays;

    public static Employee Create(
        string fullName,
        Nik nik,
        Money monthlyWage,
        LocalDate effectiveSalaryFrom,
        EmployeeRole role,
        EmployeeId? parentId = null,
        Npwp? npwp = null,
        EmployeeId? id = null,
        IReadOnlyCollection<EmployeeId>? parentAncestors = null,
        LocalDate? hireDate = null)
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

        ValidateParentChain(employeeId, parentId, parentAncestors ?? Array.Empty<EmployeeId>());

        var employee = new Employee(
            employeeId,
            fullName.Trim(),
            nik,
            npwp,
            monthlyWage,
            effectiveSalaryFrom,
            role,
            parentId,
            hireDate);

        employee.RaiseDomainEvent(new EmployeeCreated(
            employee.Id.Value,
            employee.FullName,
            nik.Value,
            npwp?.Value,
            role,
            parentId?.Value,
            monthlyWage,
            effectiveSalaryFrom,
            hireDate));
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

    public void AssignParent(
        EmployeeId? newParentId,
        IReadOnlyCollection<EmployeeId>? newParentAncestors = null)
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

        ValidateParentChain(Id, newParentId, newParentAncestors ?? Array.Empty<EmployeeId>());

        if (ParentId == newParentId)
        {
            return;
        }

        var old = ParentId;
        ParentId = newParentId;
        RaiseDomainEvent(new EmployeeParentChanged(Id.Value, old?.Value, newParentId?.Value));
    }

    private static void ValidateParentChain(
        EmployeeId selfId,
        EmployeeId? parentId,
        IReadOnlyCollection<EmployeeId> parentAncestors)
    {
        if (!parentId.HasValue)
        {
            return;
        }

        if (parentId.Value == selfId)
        {
            throw new DomainException(
                "employee.parent_cycle",
                "Assigning this parent would create a cycle in the hierarchy.");
        }

        foreach (var ancestor in parentAncestors)
        {
            if (ancestor == selfId)
            {
                throw new DomainException(
                    "employee.parent_cycle",
                    "Assigning this parent would create a cycle in the hierarchy.");
            }
        }

        var depth = parentAncestors.Count + 1;
        if (depth > EmployeeHierarchyPolicy.MaxDepth)
        {
            throw new DomainException(
                "employee.depth_exceeded",
                $"Employee hierarchy depth cannot exceed {EmployeeHierarchyPolicy.MaxDepth}.");
        }
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

    /// <summary>
    /// Reflects whether approved leave covers today. Driven by the leave module rather than
    /// set by hand, and deliberately silent on a terminated employee: termination is the end
    /// of the record, not a state leave gets to flip in and out of.
    /// </summary>
    public void SetOnLeave(bool onLeave)
    {
        if (Status == EmployeeStatus.Terminated)
        {
            return;
        }

        Status = onLeave ? EmployeeStatus.OnLeave : EmployeeStatus.Active;
    }

    /// <summary>
    /// Owner-only. Moving the hire date moves the *default* probation end with it, but never an
    /// override an Owner set deliberately — that is what <see cref="OverrideProbationEnd"/> is for.
    /// </summary>
    public void SetHireDate(LocalDate? hireDate)
    {
        EnsureActive();
        if (HireDate == hireDate)
        {
            return;
        }

        var old = HireDate;
        HireDate = hireDate;
        RaiseDomainEvent(new EmployeeHireDateChanged(Id.Value, old, hireDate));
    }

    /// <summary>
    /// Owner-only. Null clears the override and falls back to the three-month default.
    /// Accepts any date, including one in the past — an Owner confirming someone early is a
    /// legitimate correction, and the request workflow is what enforces "later than now".
    /// </summary>
    public void OverrideProbationEnd(LocalDate? endsOn)
    {
        EnsureActive();
        if (ProbationEndsOnOverride == endsOn)
        {
            return;
        }

        var oldEffective = ProbationEndsOn;
        ProbationEndsOnOverride = endsOn;
        RaiseDomainEvent(new EmployeeProbationEndChanged(Id.Value, oldEffective, ProbationEndsOn));
    }

    /// <summary>
    /// Owner-only. Null <paramref name="entitledDays"/> clears the override, returning the type
    /// to the default (the computed formula for Annual, uncapped for everything else).
    /// Zero is a real value and means "none of this type".
    /// </summary>
    public void SetLeaveQuota(LeaveType type, int? entitledDays)
    {
        EnsureActive();
        if (entitledDays is < 0)
        {
            throw new DomainException("employee.quota_negative", "Entitled days cannot be negative.");
        }

        var existing = _leaveQuotas.FirstOrDefault(quota => quota.Type == type);
        var old = existing?.EntitledDays;
        if (old == entitledDays)
        {
            return;
        }

        if (entitledDays is not { } days)
        {
            _leaveQuotas.Remove(existing!);
        }
        else if (existing is null)
        {
            _leaveQuotas.Add(new EmployeeLeaveQuota(type, days));
        }
        else
        {
            existing.SetEntitledDays(days);
        }

        RaiseDomainEvent(new EmployeeLeaveQuotaChanged(Id.Value, type, old, entitledDays));
    }

    private void EnsureActive()
    {
        if (Status == EmployeeStatus.Terminated)
        {
            throw new DomainException("employee.terminated", "Cannot modify a terminated employee.");
        }
    }
}
