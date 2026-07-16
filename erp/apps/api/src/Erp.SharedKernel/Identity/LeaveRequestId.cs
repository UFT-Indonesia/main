namespace Erp.SharedKernel.Identity;

public readonly record struct LeaveRequestId(Guid Value)
{
    public static LeaveRequestId Empty => new(Guid.Empty);

    public static LeaveRequestId New() => new(Guid.NewGuid());
}
