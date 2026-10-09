using Erp.SharedKernel.Domain.Errors;
using NodaTime;

namespace Erp.Core.Aggregates.Payroll;

/// <summary>
/// The single payroll settings row (id 1). The divisor is what a monthly salary is divided by to
/// price one leave day (GSS03 decision 1, default 20); <see cref="FirstMonth"/> is the launch month,
/// the first one that can be closed — before it, over-cap leave was refused, so nothing exists to cut.
/// </summary>
public sealed class PayrollSettings
{
    public const int SingletonId = 1;
    public const int DefaultDivisor = 20;

    /// <summary>A month has at most 31 days; anything above is a typo, not a policy.</summary>
    public const int MaxDivisor = 31;

    // EF Core constructor.
    private PayrollSettings() { }

    public PayrollSettings(LocalDate firstMonth)
    {
        Id = SingletonId;
        Divisor = DefaultDivisor;
        FirstMonth = firstMonth;
    }

    public int Id { get; private set; }

    public int Divisor { get; private set; }

    public LocalDate FirstMonth { get; private set; }

    public void SetDivisor(int divisor)
    {
        if (divisor is <= 0 or > MaxDivisor)
        {
            throw new DomainException("payroll.divisor", $"Divisor must be between 1 and {MaxDivisor}.");
        }

        Divisor = divisor;
    }
}

/// <summary>One change of the company divisor (GSS03 follow-up Q5): what it was, what it became, who and when.</summary>
public sealed class PayrollSettingsChange
{
    // EF Core constructor.
    private PayrollSettingsChange() { }

    public PayrollSettingsChange(int oldDivisor, int newDivisor, Guid changedByUserId, string changedByName, Instant changedAtUtc)
    {
        Id = Guid.NewGuid();
        OldDivisor = oldDivisor;
        NewDivisor = newDivisor;
        ChangedByUserId = changedByUserId;
        ChangedByName = changedByName;
        ChangedAtUtc = changedAtUtc;
    }

    public Guid Id { get; private set; }

    public int OldDivisor { get; private set; }

    public int NewDivisor { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public string ChangedByName { get; private set; } = default!;

    public Instant ChangedAtUtc { get; private set; }
}
