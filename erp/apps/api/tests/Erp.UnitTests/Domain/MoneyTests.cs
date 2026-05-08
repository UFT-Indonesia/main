using Erp.Core.Aggregates.Common;
using Erp.SharedKernel.Domain.Errors;
using FluentAssertions;

namespace Erp.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Idr_rounds_to_whole_rupiah()
    {
        var money = Money.Idr(1234.6m);

        money.Amount.Should().Be(1235m);
        money.Currency.Should().Be(Money.IDR);
    }

    [Fact]
    public void Idr_rejects_negative()
    {
        var act = () => Money.Idr(-1m);

        act.Should().Throw<DomainException>().Where(e => e.Code == "money.negative");
    }

    [Fact]
    public void Add_combines_same_currency()
    {
        var a = Money.Idr(1_000_000m);
        var b = Money.Idr(500_000m);

        a.Add(b).Should().Be(Money.Idr(1_500_000m));
    }

    [Fact]
    public void Subtract_throws_when_result_negative()
    {
        var a = Money.Idr(100m);
        var b = Money.Idr(200m);

        var act = () => a.Subtract(b);

        act.Should().Throw<DomainException>().Where(e => e.Code == "money.negative");
    }

    [Fact]
    public void Multiply_scales_amount()
    {
        var hadirPremi = Money.Idr(5_000m);

        hadirPremi.Multiply(20).Should().Be(Money.Idr(100_000m));
    }

    [Fact]
    public void Equality_uses_amount_and_currency()
    {
        Money.Idr(1_000m).Should().Be(Money.Idr(1_000m));
        Money.Idr(1_000m).Should().NotBe(Money.Idr(1_001m));
    }
}
