using Erp.SharedKernel.Domain.Errors;

namespace Erp.Core.Aggregates.Employees;

/// <summary>
/// An Owner's per-employee override of how a cut leave day is priced (GSS03 decision 11): either a
/// flat amount per cut day (Rp 0 exempts the employee) or a custom divisor. One setting covers
/// every leave type; the absence of one means the company divisor.
/// </summary>
public sealed record LeaveDeductionException
{
    // EF Core constructor.
    private LeaveDeductionException() { }

    private LeaveDeductionException(decimal? flatAmountPerDay, int? divisor)
    {
        FlatAmountPerDay = flatAmountPerDay;
        Divisor = divisor;
    }

    public decimal? FlatAmountPerDay { get; private init; }

    public int? Divisor { get; private init; }

    /// <summary>Null when both are empty: nothing to store, the company divisor applies.</summary>
    public static LeaveDeductionException? Create(decimal? flatAmountPerDay, int? divisor)
    {
        if (flatAmountPerDay is not null && divisor is not null)
        {
            throw new DomainException(
                "employee.deduction_exception", "Set a flat amount or a custom divisor, not both.");
        }

        if (flatAmountPerDay is < 0)
        {
            throw new DomainException("employee.deduction_flat_negative", "Flat amount cannot be negative.");
        }

        if (divisor is <= 0 or > Payroll.PayrollSettings.MaxDivisor)
        {
            throw new DomainException(
                "employee.deduction_divisor", $"Divisor must be between 1 and {Payroll.PayrollSettings.MaxDivisor}.");
        }

        return flatAmountPerDay is null && divisor is null ? null : new LeaveDeductionException(flatAmountPerDay, divisor);
    }
}
