namespace Erp.SharedKernel.Identity;

public readonly record struct AttendancePolicyId(Guid Value)
{
    public static AttendancePolicyId Empty => new(Guid.Empty);

    public static AttendancePolicyId New() => new(Guid.NewGuid());

    /// <summary>
    /// Fixed id for the single global attendance policy row (singleton-by-construction —
    /// there is exactly one policy in this system, no per-employee/per-shift assignment).
    /// </summary>
    public static AttendancePolicyId Singleton => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
}
