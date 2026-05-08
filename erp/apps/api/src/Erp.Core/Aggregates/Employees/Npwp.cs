using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;

namespace Erp.Core.Aggregates.Employees;

public sealed class Npwp : ValueObject
{
    public const int LegacyLength = 15;
    public const int NewLength = 16;

    private Npwp(string normalized)
    {
        Value = normalized;
    }

    public string Value { get; }

    public static Npwp Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("npwp.empty", "NPWP is required.");
        }

        if (value.Any(c => !char.IsDigit(c) && c != '.' && c != '-' && !char.IsWhiteSpace(c)))
        {
            throw new DomainException("npwp.format", "NPWP contain only digits and separators(., -, whitespace).");
        }

        // Strip common separators (".", "-", whitespace) before validating digit length.
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != LegacyLength && digits.Length != NewLength)
        {
            throw new DomainException(
                "npwp.length",
                $"NPWP must be {LegacyLength} or {NewLength} digits.");
        }

        return new Npwp(digits);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
