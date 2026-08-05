namespace Erp.Infrastructure.Identity;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Seed:Owner";

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// NIK for the owner's employee record. Placeholder by default — the owner is a real
    /// person whose actual NIK should be set here (or corrected in the UI afterwards),
    /// but an employee row has to exist before anyone can hold leave or be an approver.
    /// </summary>
    public string Nik { get; init; } = "0000000000000001";

    /// <summary>Nominal, so the aggregate's "wage must be positive" rule holds; set the real figure in the UI.</summary>
    public decimal MonthlyWage { get; init; } = 1m;

    public bool HasOwnerCredentials =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(FullName);
}
