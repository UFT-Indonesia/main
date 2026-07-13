namespace Erp.SharedKernel.Identity;

public readonly record struct AttendanceDayId(Guid Value)
{
    public static AttendanceDayId Empty => new(Guid.Empty);

    public static AttendanceDayId New() => new(Guid.NewGuid());
}
