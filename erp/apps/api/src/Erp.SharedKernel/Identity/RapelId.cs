namespace Erp.SharedKernel.Identity;

public readonly record struct RapelId(Guid Value)
{
    public static RapelId Empty => new(Guid.Empty);

    public static RapelId New() => new(Guid.NewGuid());
}
