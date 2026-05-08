using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;

namespace Erp.Core.Aggregates.Employees;

public sealed class Nik : ValueObject
{
    public const int Length = 16;

    private Nik(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Nik Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("nik.empty", "NIK is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length != Length)
        {
            throw new DomainException("nik.length", $"NIK must be {Length} digits.");
        }

        if (!trimmed.All(char.IsDigit))
        {
            throw new DomainException("nik.format", "NIK must contain digits only.");
        }

        return new Nik(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
