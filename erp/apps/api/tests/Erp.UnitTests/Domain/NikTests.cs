using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Domain.Errors;
using FluentAssertions;

namespace Erp.UnitTests.Domain;

public class NikTests
{
    [Fact]
    public void Create_accepts_16_digit_string()
    {
        var nik = Nik.Create("3201234567890123");

        nik.Value.Should().Be("3201234567890123");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty(string value)
    {
        var act = () => Nik.Create(value);

        act.Should().Throw<DomainException>().Where(e => e.Code == "nik.empty");
    }

    [Theory]
    [InlineData("123456789012345")] // 15
    [InlineData("12345678901234567")] // 17
    public void Create_rejects_wrong_length(string value)
    {
        var act = () => Nik.Create(value);

        act.Should().Throw<DomainException>().Where(e => e.Code == "nik.length");
    }

    [Fact]
    public void Create_rejects_non_digit()
    {
        var act = () => Nik.Create("3201234567890ABC");

        act.Should().Throw<DomainException>().Where(e => e.Code == "nik.format");
    }
}
