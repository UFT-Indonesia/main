using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;

namespace Erp.Core.Aggregates.Common;

public sealed class Money : ValueObject
{
    public const string IDR = "IDR";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Idr(decimal amount)
    {
        if (amount < 0m)
        {
            throw new DomainException("money.negative", "Amount cannot be negative.");
        }

        // IDR has no minor units; round to whole rupiah.
        return new Money(decimal.Round(amount, 0, MidpointRounding.AwayFromZero), IDR);
    }

    public static Money Zero(string currency = IDR) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0m)
        {
            throw new DomainException("money.negative", "Amount cannot be negative.");
        }

        return new Money(result, Currency);
    }

    public Money Multiply(int factor)
    {
        if (factor < 0)
        {
            throw new DomainException("money.negative", "Factor cannot be negative.");
        }

        return new Money(Amount * factor, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                "money.currency_mismatch",
                $"Currency mismatch: {Currency} vs {other.Currency}.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Currency} {Amount:N0}";
}
