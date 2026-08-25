namespace Erp.SharedKernel.Identity;

public readonly record struct ProbationExtensionRequestId(Guid Value)
{
    public static ProbationExtensionRequestId Empty => new(Guid.Empty);

    public static ProbationExtensionRequestId New() => new(Guid.NewGuid());
}
