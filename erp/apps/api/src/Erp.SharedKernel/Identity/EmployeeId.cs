namespace Erp.SharedKernel.Identity;

public readonly record struct EmployeeId(Guid Value)
{
    public static EmployeeId Empty => new(Guid.Empty);

    public static EmployeeId New() => new(Guid.NewGuid());
}
