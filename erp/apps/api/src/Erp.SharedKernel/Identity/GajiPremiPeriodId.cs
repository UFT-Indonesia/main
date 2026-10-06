namespace Erp.SharedKernel.Identity;

public readonly record struct GajiPremiPeriodId(Guid Value)
{
    public static GajiPremiPeriodId Empty => new(Guid.Empty);

    public static GajiPremiPeriodId New() => new(Guid.NewGuid());
}
