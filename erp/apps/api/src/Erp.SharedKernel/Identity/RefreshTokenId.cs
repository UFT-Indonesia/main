namespace Erp.SharedKernel.Identity;

public readonly record struct RefreshTokenId(Guid Value)
{
    public static RefreshTokenId Empty => new(Guid.Empty);

    public static RefreshTokenId New() => new(Guid.NewGuid());
}
