namespace Erp.SharedKernel.Identity;

public readonly record struct OvertimeCorrectionRequestId(Guid Value)
{
    public static OvertimeCorrectionRequestId Empty => new(Guid.Empty);

    public static OvertimeCorrectionRequestId New() => new(Guid.NewGuid());
}
