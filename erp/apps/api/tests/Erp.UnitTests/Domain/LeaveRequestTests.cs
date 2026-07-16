using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.Domain;

public class LeaveRequestTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Decider = Guid.NewGuid();

    private static LeaveRequest PendingRequest(
        LocalDate? start = null,
        LocalDate? end = null) =>
        LeaveRequest.Create(
            EmployeeId.New(),
            LeaveType.Annual,
            start ?? new LocalDate(2026, 8, 3), // Monday
            end ?? new LocalDate(2026, 8, 7),   // Friday
            "acara keluarga",
            Requester,
            Now);

    [Fact]
    public void Create_computes_workdays_and_starts_pending()
    {
        var request = PendingRequest();

        request.Status.Should().Be(LeaveRequestStatus.Pending);
        request.WorkdayCount.Should().Be(5);
        request.Reason.Should().Be("acara keluarga");
        request.RequestedByUserId.Should().Be(Requester);
        request.DecidedByUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(2026, 8, 7, 2026, 8, 10, 2)]   // Fri–Mon: weekend skipped
    [InlineData(2026, 8, 3, 2026, 8, 3, 1)]    // single Monday
    [InlineData(2026, 7, 27, 2026, 8, 9, 10)]  // two full weeks
    public void CountWorkdays_skips_weekends(int y1, int m1, int d1, int y2, int m2, int d2, int expected)
    {
        LeaveRequest.CountWorkdays(new LocalDate(y1, m1, d1), new LocalDate(y2, m2, d2))
            .Should().Be(expected);
    }

    [Fact]
    public void Create_rejects_weekend_only_range()
    {
        var act = () => PendingRequest(new LocalDate(2026, 8, 8), new LocalDate(2026, 8, 9)); // Sat–Sun

        act.Should().Throw<DomainException>().Where(e => e.Code == "leave.no_workdays");
    }

    [Fact]
    public void Create_rejects_inverted_range()
    {
        var act = () => PendingRequest(new LocalDate(2026, 8, 7), new LocalDate(2026, 8, 3));

        act.Should().Throw<DomainException>().Where(e => e.Code == "leave.date_range");
    }

    [Fact]
    public void Approve_sets_status_and_decision_audit()
    {
        var request = PendingRequest();

        request.Approve(Decider, "Budi", Now);

        request.Status.Should().Be(LeaveRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(Decider);
        request.DecidedByName.Should().Be("Budi");
        request.DecidedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Deny_records_note()
    {
        var request = PendingRequest();

        request.Deny(Decider, "Budi", Now, "peak season");

        request.Status.Should().Be(LeaveRequestStatus.Denied);
        request.DecisionNote.Should().Be("peak season");
    }

    [Fact]
    public void Approve_rejects_already_decided()
    {
        var request = PendingRequest();
        request.Deny(Decider, "Budi", Now, null);

        var act = () => request.Approve(Decider, "Budi", Now);

        act.Should().Throw<DomainException>().Where(e => e.Code == "leave.not_pending");
    }

    [Fact]
    public void Cancel_allowed_on_pending_and_approved_but_not_denied()
    {
        var pending = PendingRequest();
        pending.Cancel(Decider, "Budi", Now, null);
        pending.Status.Should().Be(LeaveRequestStatus.Cancelled);

        var approved = PendingRequest();
        approved.Approve(Decider, "Budi", Now);
        approved.Cancel(Decider, "Budi", Now, "trip cancelled");
        approved.Status.Should().Be(LeaveRequestStatus.Cancelled);
        approved.DecisionNote.Should().Be("trip cancelled");

        var denied = PendingRequest();
        denied.Deny(Decider, "Budi", Now, null);
        var act = () => denied.Cancel(Decider, "Budi", Now, null);
        act.Should().Throw<DomainException>().Where(e => e.Code == "leave.not_cancellable");
    }

    [Theory]
    [InlineData(2026, 8, 1, 2026, 8, 3, true)]   // overlaps start
    [InlineData(2026, 8, 7, 2026, 8, 10, true)]  // overlaps end
    [InlineData(2026, 8, 4, 2026, 8, 5, true)]   // inside
    [InlineData(2026, 8, 1, 2026, 8, 10, true)]  // envelops
    [InlineData(2026, 8, 10, 2026, 8, 12, false)] // after
    [InlineData(2026, 7, 30, 2026, 8, 2, false)]  // before
    public void Overlaps_matches_inclusive_ranges(int y1, int m1, int d1, int y2, int m2, int d2, bool expected)
    {
        // Request range: 3–7 Aug 2026.
        PendingRequest().Overlaps(new LocalDate(y1, m1, d1), new LocalDate(y2, m2, d2))
            .Should().Be(expected);
    }
}
