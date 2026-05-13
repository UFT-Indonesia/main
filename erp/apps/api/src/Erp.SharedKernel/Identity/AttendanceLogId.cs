namespace Erp.SharedKernel.Identity;

public readonly record struct AttendanceLogId(Guid Value)
{
    public static AttendanceLogId Empty => new(Guid.Empty);

    public static AttendanceLogId New() => new(Guid.NewGuid());
}
