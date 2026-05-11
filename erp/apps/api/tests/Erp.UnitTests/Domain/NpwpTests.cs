using Erp.Core.Aggregates.Employees;
using Erp.SharedKernel.Domain.Errors;
using FluentAssertions;

namespace Erp.UnitTests.Domain;

public class NpwpTests
{
    [Theory]
    [InlineData("12.345.678.9-012.000", "123456789012000")]
    [InlineData("123456789012000", "123456789012000")]
    [InlineData("1234567890123456", "1234567890123456")]
    public void Create_strips_separators_and_keeps_digits(string input, string expected)
    {
        Npwp.Create(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12345678901234")] // 14
    [InlineData("12345678901234567")] // 17
    public void Create_rejects_invalid_length(string input)
    {
        var act = () => Npwp.Create(input);

        act.Should().Throw<DomainException>().Where(e => e.Code == "npwp.length");
    }

    [Theory]
    [InlineData("12.345.678.9-012.A00")]
    [InlineData("12345678901200/")]
    [InlineData("12345678901200_")]
    public void Create_rejects_invalid_format(string input)
    {
        var act = () => Npwp.Create(input);

        act.Should().Throw<DomainException>().Where(e => e.Code == "npwp.format");
    }

    [Fact]
    public void Create_rejects_empty()
    {
        var act = () => Npwp.Create("   ");

        act.Should().Throw<DomainException>().Where(e => e.Code == "npwp.empty");
    }
}
