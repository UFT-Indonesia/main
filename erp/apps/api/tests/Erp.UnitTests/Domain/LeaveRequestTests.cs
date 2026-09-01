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
            null,
            halfDay: false,
            halfDayPeriod: null,
            startHour: null,
            endHour: null,
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
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("x")]        // one character is below ReasonMinLength
    public void Create_requires_a_reason(string? reason)
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(),
            LeaveType.Annual,
            new LocalDate(2026, 8, 3),
            new LocalDate(2026, 8, 7),
            reason!,
            null,
            halfDay: false,
            halfDayPeriod: null,
            startHour: null,
            endHour: null,
            Requester,
            Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.reason_required");
    }

    [Fact]
    public void Create_trims_the_reason()
    {
        var request = LeaveRequest.Create(
            EmployeeId.New(),
            LeaveType.Sick,
            new LocalDate(2026, 8, 3),
            new LocalDate(2026, 8, 3),
            "  demam  ",
            TestAttachments.DoctorsNote(),
            halfDay: false,
            halfDayPeriod: null,
            startHour: null,
            endHour: null,
            Requester,
            Now);

        request.Reason.Should().Be("demam");
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
        pending.Cancel(Decider, "Budi", Now, null, LeaveCancellationReason.WithdrawnByEmployee);
        pending.Status.Should().Be(LeaveRequestStatus.Cancelled);

        var approved = PendingRequest();
        approved.Approve(Decider, "Budi", Now);
        approved.Cancel(Decider, "Budi", Now, "trip cancelled", LeaveCancellationReason.RecalledForWork);
        approved.Status.Should().Be(LeaveRequestStatus.Cancelled);
        approved.DecisionNote.Should().Be("trip cancelled");
        approved.CancellationReason.Should().Be(LeaveCancellationReason.RecalledForWork);

        var denied = PendingRequest();
        denied.Deny(Decider, "Budi", Now, null);
        var act = () => denied.Cancel(Decider, "Budi", Now, null, LeaveCancellationReason.WithdrawnByEmployee);
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

    // ---- half-day / hourly validation ------------------------------------

    [Fact]
    public void Half_day_requires_a_type_of_Annual()
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Sick,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "sakit", TestAttachments.DoctorsNote(),
            halfDay: true, halfDayPeriod: HalfDayPeriod.Morning, startHour: null, endHour: null,
            Requester, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.half_day_not_allowed");
    }

    [Fact]
    public void Half_day_requires_choosing_a_period()
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "acara", null,
            halfDay: true, halfDayPeriod: null, startHour: null, endHour: null,
            Requester, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.half_day_period");
    }

    [Fact]
    public void Hourly_bounds_require_a_type_of_Permission()
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "acara", null,
            halfDay: false, halfDayPeriod: null, startHour: 10, endHour: 11,
            Requester, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.hourly_not_allowed");
    }

    [Theory]
    [InlineData(12, 14)]  // 12 itself is excluded from the boundary set
    [InlineData(9, 12)]   // 12 excluded even as an end boundary
    public void Hourly_bounds_must_come_from_the_allowed_set(int start, int end)
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Permission,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "izin", null,
            halfDay: false, halfDayPeriod: null, startHour: start, endHour: end,
            Requester, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.hourly_range_invalid");
    }

    [Fact]
    public void Hourly_start_must_be_before_end()
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Permission,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "izin", null,
            halfDay: false, halfDayPeriod: null, startHour: 11, endHour: 10,
            Requester, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.hourly_range_invalid");
    }

    [Fact]
    public void Hourly_range_cannot_cross_the_lunch_hour()
    {
        var act = () => LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Permission,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "izin", null,
            halfDay: false, halfDayPeriod: null, startHour: 11, endHour: 14,
            Requester, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("leave.hourly_range_crosses_lunch");
    }

    [Theory]
    [InlineData(9, 11)]   // morning side
    [InlineData(13, 17)]  // afternoon side
    public void Hourly_range_on_one_side_of_lunch_is_accepted(int start, int end)
    {
        var request = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Permission,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "izin", null,
            halfDay: false, halfDayPeriod: null, startHour: start, endHour: end,
            Requester, Now);

        request.StartHour.Should().Be(start);
        request.EndHour.Should().Be(end);
    }

    // ---- charge and occupied window ---------------------------------------

    [Fact]
    public void A_plain_request_charges_one_full_day_and_occupies_the_whole_shift()
    {
        var request = PendingRequest();

        request.ChargePerWorkday(TestPolicies.Standard).Should().Be(1m);
        request.OccupiedWindow(TestPolicies.Standard).Should().Be((new LocalTime(9, 0), new LocalTime(18, 0)));
    }

    [Fact]
    public void A_half_day_charges_half_and_occupies_only_its_own_side()
    {
        var morning = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "acara", null,
            halfDay: true, halfDayPeriod: HalfDayPeriod.Morning, startHour: null, endHour: null,
            Requester, Now);

        morning.ChargePerWorkday(TestPolicies.Standard).Should().Be(0.5m);
        morning.OccupiedWindow(TestPolicies.Standard).Should().Be((new LocalTime(9, 0), new LocalTime(12, 0)));

        var afternoon = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "acara", null,
            halfDay: true, halfDayPeriod: HalfDayPeriod.Afternoon, startHour: null, endHour: null,
            Requester, Now);

        afternoon.OccupiedWindow(TestPolicies.Standard).Should().Be((new LocalTime(13, 0), new LocalTime(18, 0)));
    }

    [Fact]
    public void An_hourly_request_charges_its_fraction_of_the_net_working_day()
    {
        // 09:00–18:00 shift minus the 1-hour lunch = 8 net hours. 2 hours taken = 2/8.
        var request = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Permission,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
            "izin", null,
            halfDay: false, halfDayPeriod: null, startHour: 9, endHour: 11,
            Requester, Now);

        request.ChargePerWorkday(TestPolicies.Standard).Should().Be(2m / 8m);
        request.OccupiedWindow(TestPolicies.Standard).Should().Be((new LocalTime(9, 0), new LocalTime(11, 0)));
    }

    [Fact]
    public void Total_charge_multiplies_the_per_day_charge_by_every_workday_covered()
    {
        // Mon 3 – Wed 5 Aug 2026: 3 workdays, half day each.
        var request = LeaveRequest.Create(
            EmployeeId.New(), LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 5),
            "acara", null,
            halfDay: true, halfDayPeriod: HalfDayPeriod.Morning, startHour: null, endHour: null,
            Requester, Now);

        request.TotalCharge(TestPolicies.Standard).Should().Be(1.5m);
    }

    [Theory]
    [InlineData(9, 0, 12, 0, 13, 0, 18, 0, false)]  // morning vs afternoon: no touch
    [InlineData(9, 0, 12, 0, 11, 0, 14, 0, true)]   // morning vs a range that spills into the afternoon
    [InlineData(14, 0, 16, 0, 16, 0, 18, 0, false)] // back-to-back, end == start: not an intersection
    [InlineData(14, 0, 16, 0, 15, 0, 17, 0, true)]  // genuinely overlapping
    public void WindowsIntersect_matches_half_open_interval_overlap(
        int aH1, int aM1, int aH2, int aM2, int bH1, int bM1, int bH2, int bM2, bool expected)
    {
        LeaveRequest.WindowsIntersect(
            (new LocalTime(aH1, aM1), new LocalTime(aH2, aM2)),
            (new LocalTime(bH1, bM1), new LocalTime(bH2, bM2)))
            .Should().Be(expected);
    }
}
