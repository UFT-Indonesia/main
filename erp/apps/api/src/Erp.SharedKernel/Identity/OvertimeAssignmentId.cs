namespace Erp.SharedKernel.Identity;

public readonly record struct OvertimeAssignmentId(Guid Value)
{
    public static OvertimeAssignmentId Empty => new(Guid.Empty);

    public static OvertimeAssignmentId New() => new(Guid.NewGuid());
}
