namespace Erp.Infrastructure.Identity;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Seed:Owner";

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public bool HasOwnerCredentials =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(FullName);
}
