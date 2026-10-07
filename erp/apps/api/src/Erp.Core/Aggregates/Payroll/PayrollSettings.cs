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
