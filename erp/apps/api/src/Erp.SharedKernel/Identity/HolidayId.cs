namespace Erp.SharedKernel.Identity;

public readonly record struct HolidayId(Guid Value)
{
    public static HolidayId Empty => new(Guid.Empty);

    public static HolidayId New() => new(Guid.NewGuid());
}
