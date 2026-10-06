using Erp.Core.Aggregates.Overtime;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

/// <summary>The suggested amount of a rapel: tier of the claimed times minus what the assignment already paid.</summary>
public class RapelTests
{
    private static readonly OvertimeTier[] Tiers = [new(2, 25_000), new(4, 50_000), new(6, 70_000)];
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 2, 3, 0);

    private static OvertimeAssignment Closed(LocalDate date, LocalTime? start, LocalTime end, bool dayOff, decimal paid)
    {
        var a = OvertimeAssignment.Create(EmployeeId.New(), date, start, end, dayOff, Guid.NewGuid(), "Owner", Now, autoApprove: true);
        a.Freeze(0, paid, Now);
        return a;
    }

    private static Rapel Claim(OvertimeAssignment a, int fromH, int fromM, int toH, int toM) =>
        Rapel.Create(a, new LocalTime(fromH, fromM), new LocalTime(toH, toM), "lupa tap out, ada chat WA",
            TestAttachments.DoctorsNote(), TestPolicies.Standard, Tiers, Guid.NewGuid(), Now);

    [Fact]
    public void Never_paid_evening_suggests_the_full_tier()
    {
        var rapel = Claim(Closed(new LocalDate(2026, 10, 6), null, new LocalTime(21, 30), false, 0m), 18, 30, 21, 30);

        rapel.ClaimedHours.Should().Be(3);
        rapel.SuggestedAmount.Should().Be(25_000);
    }

    [Fact]
    public void Partly_paid_evening_suggests_only_the_difference()
    {
        var rapel = Claim(Closed(new LocalDate(2026, 10, 6), null, new LocalTime(1, 0), false, 25_000m), 18, 30, 0, 45);

        rapel.ClaimedHours.Should().Be(6, "00:45 is the next morning");
        rapel.SuggestedAmount.Should().Be(45_000);
    }

    [Fact]
    public void Claimed_times_are_clamped_to_the_window_and_lose_the_day_off_lunch()
    {
        // Sat 09:00–16:00 assigned; claiming 07:00–19:00 still counts 7h − 1h lunch = 6h.
        var rapel = Claim(Closed(new LocalDate(2026, 10, 10), new LocalTime(9, 0), new LocalTime(16, 0), true, 0m), 7, 0, 19, 0);

        rapel.ClaimedHours.Should().Be(6);
        rapel.SuggestedAmount.Should().Be(70_000);
    }

    [Fact]
    public void A_claim_that_pays_no_more_than_already_paid_is_refused()
    {
        var a = Closed(new LocalDate(2026, 10, 6), null, new LocalTime(21, 30), false, 25_000m);

        FluentActions.Invoking(() => Claim(a, 18, 30, 21, 30))
            .Should().Throw<DomainException>().Which.Code.Should().Be("rapel.adds_nothing");
    }

    [Fact]
    public void Only_a_closed_approved_assignment_can_be_claimed()
    {
        var open = OvertimeAssignment.Create(
            EmployeeId.New(), new LocalDate(2026, 10, 6), null, new LocalTime(21, 30), false, Guid.NewGuid(), "Owner", Now, autoApprove: true);

        FluentActions.Invoking(() => Claim(open, 18, 30, 21, 30))
            .Should().Throw<DomainException>().Which.Code.Should().Be("rapel.not_claimable");
    }

    [Fact]
    public void Approving_without_an_amount_keeps_the_suggestion_and_an_owner_figure_overrides_it()
    {
        var a = Closed(new LocalDate(2026, 10, 6), null, new LocalTime(21, 30), false, 0m);
        var kept = Claim(a, 18, 30, 21, 30);
        var overridden = Claim(a, 18, 30, 21, 30);

        kept.Approve(null, new LocalDate(2026, 11, 1), Guid.NewGuid(), "Owner", Now);
        overridden.Approve(30_000, new LocalDate(2026, 11, 1), Guid.NewGuid(), "Owner", Now);

        kept.Amount.Should().Be(25_000);
        overridden.Amount.Should().Be(30_000);
    }
}
