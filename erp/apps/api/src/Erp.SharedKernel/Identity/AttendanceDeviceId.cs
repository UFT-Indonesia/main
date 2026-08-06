namespace Erp.SharedKernel.Identity;

public readonly record struct AttendanceDeviceId(Guid Value)
{
    public static AttendanceDeviceId Empty => new(Guid.Empty);

    public static AttendanceDeviceId New() => new(Guid.NewGuid());
}
