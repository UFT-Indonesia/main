using Erp.Core.Aggregates.Leave;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Leave.Common;
using FluentAssertions;
using NodaTime;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// Half-day/hourly detail must follow the same visibility rule as Type/Reason — a non-null
/// StartHour or HalfDay would otherwise prove the hidden Type (only Permission sets hours, only
/// Annual sets HalfDay), defeating the redaction sitting right next to it.
/// </summary>
public class LeaveRequestResultTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

    private static LeaveRequest HourlyIzin() => LeaveRequest.Create(
        EmployeeId.New(), LeaveType.Permission,
        new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
        "izin", null, halfDay: false, halfDayPeriod: null, startHour: 9, endHour: 11,
        Guid.NewGuid(), Now);

    private static LeaveRequest HalfDayAnnual() => LeaveRequest.Create(
        EmployeeId.New(), LeaveType.Annual,
        new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 3),
        "acara", null, halfDay: true, halfDayPeriod: HalfDayPeriod.Morning,
        startHour: null, endHour: null, Guid.NewGuid(), Now);

    [Fact]
    public void Hourly_detail_is_hidden_when_details_are_not_readable()
    {
        var result = LeaveRequestResult.From(HourlyIzin(), TestPolicies.Standard, canReadDetails: false);

        result.Type.Should().BeNull();
        result.StartHour.Should().BeNull();
        result.EndHour.Should().BeNull();
        result.HalfDay.Should().BeFalse();
        result.ChargedDays.Should().BeNull();
    }

    [Fact]
    public void Hourly_detail_is_shown_when_details_are_readable()
    {
        var result = LeaveRequestResult.From(HourlyIzin(), TestPolicies.Standard, canReadDetails: true);

        result.StartHour.Should().Be(9);
        result.EndHour.Should().Be(11);
        result.ChargedDays.Should().NotBeNull();
    }

    [Fact]
    public void Half_day_detail_is_hidden_when_details_are_not_readable()
    {
        var result = LeaveRequestResult.From(HalfDayAnnual(), TestPolicies.Standard, canReadDetails: false);

        result.HalfDay.Should().BeFalse();
        result.HalfDayPeriod.Should().BeNull();
        result.ChargedDays.Should().BeNull();
    }

    [Fact]
    public void Half_day_detail_is_shown_when_details_are_readable()
    {
        var result = LeaveRequestResult.From(HalfDayAnnual(), TestPolicies.Standard, canReadDetails: true);

        result.HalfDay.Should().BeTrue();
        result.HalfDayPeriod.Should().Be("Morning");
        result.ChargedDays.Should().Be(0.5m);
    }
}
