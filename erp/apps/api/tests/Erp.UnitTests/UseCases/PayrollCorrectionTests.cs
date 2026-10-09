using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.DecideLeaveRequest;
using Erp.UseCases.Leave.EditLeaveRequest;
using Erp.UseCases.Leave.Payroll;
using Erp.UseCases.Payroll;
using Erp.UseCases.Payroll.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// GSS03 follow-up: corrections after close, stamping late approvals, manual adjustments, divisor history,
/// close blockers and the payroll state a leave request carries.
/// </summary>
public class PayrollCorrectionTests
{
    // Fri 10 Apr 2026. January to March are closed, April is the first open month.
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 10, 3, 0);
    private static readonly LocalDate January = new(2026, 1, 1);
    private static readonly LocalDate March = new(2026, 3, 1);
    private static readonly LocalDate April = new(2026, 4, 1);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AttendanceDayPolicy _policy = TestPolicies.Standard;

    private readonly Employee _owner;
    private readonly Employee _manager;
    private readonly Employee _staff;
    private readonly Caller _ownerCaller;
    private readonly Caller _managerCaller;

    private readonly IRepository<LeaveDeductionLine> _lines = Substitute.For<IRepository<LeaveDeductionLine>>();
    private readonly IRepository<LeaveDeductionCorrection> _corrections = Substitute.For<IRepository<LeaveDeductionCorrection>>();
    private readonly List<LeaveDeductionLine> _existing = [];
    private readonly List<LeaveDeductionLine> _added = [];
    private readonly List<LeaveDeductionCorrection> _moved = [];
    private int _marchDivisor = 20;

    public PayrollCorrectionTests()
    {
        _clock.GetCurrentInstant().Returns(Now);

        _owner = Person("Kevin", EmployeeRole.Owner, null, "3201234567890123");
        _manager = Person("Rina", EmployeeRole.Manager, _owner.Id, "3201234567890124");
        _staff = Person("Budi", EmployeeRole.Staff, _manager.Id, "3201234567890125");
        _ownerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Owner, _owner.Id, "Kevin");
        _managerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Manager, _manager.Id, "Rina");

        // Izin capped at 2 a year, so the third day is cut. Rp 5.000.000 / 20 = Rp 250.000 a day.
        _staff.SetLeaveQuota(LeaveType.Permission, 2m);

        _lines.ListAsync(Arg.Any<ISpecification<LeaveDeductionLine>>(), Arg.Any<CancellationToken>())
            .Returns(_ => _existing.Where(l => l.IsActive).ToList());
        _lines.AddRangeAsync(Arg.Do<IEnumerable<LeaveDeductionLine>>(l => _added.AddRange(l)), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IEnumerable<LeaveDeductionLine>>());
        _corrections.AddRangeAsync(Arg.Do<IEnumerable<LeaveDeductionCorrection>>(c => _moved.AddRange(c)), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IEnumerable<LeaveDeductionCorrection>>());
    }

    private static Employee Person(string name, EmployeeRole role, EmployeeId? parentId, string nik) =>
        Employee.Create(name, Nik.Create(nik), Money.Idr(5_000_000m), new LocalDate(2025, 1, 1), role, parentId);

    private IReadRepository<LeaveDeductionMonth> ClosedMonths()
    {
        var months = Substitute.For<IReadRepository<LeaveDeductionMonth>>();
        months.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new List<LeaveDeductionMonth>
            {
                new(January, 20, Guid.NewGuid(), "Kevin", Now),
                new(new LocalDate(2026, 2, 1), 20, Guid.NewGuid(), "Kevin", Now),
                new(March, _marchDivisor, Guid.NewGuid(), "Kevin", Now),
            });
        return months;
    }

    private ClosedMonthLedger Ledger(IReadRepository<LeaveRequest>? others = null) =>
        TestPayroll.Ledger(ClosedMonths(), _lines, _corrections, others);

    /// <summary>Budi's Izin 16–18 Mar, approved; March closed with 16 and 17 free and 18 cut.</summary>
    private LeaveRequest ClosedIzin()
    {
        var request = LeaveRequest.Create(
            _staff.Id, LeaveType.Permission, new LocalDate(2026, 3, 16), new LocalDate(2026, 3, 18), "izin", null,
            false, null, null, null, Guid.NewGuid(), Instant.FromUtc(2026, 3, 2, 3, 0), _policy);
        request.Approve(_managerCaller.UserId, "Rina", Instant.FromUtc(2026, 3, 2, 3, 0));
        foreach (var (day, free, cut) in new[] { (16, 1m, 0m), (17, 1m, 0m), (18, 0m, 1m) })
        {
            _existing.Add(new LeaveDeductionLine(
                March, _staff.Id.Value, request.Id.Value, new LocalDate(2026, 3, day), LeaveType.Permission,
                free, cut, 5_000_000m, 250_000m));
        }

        return request;
    }

    private static IRepository<LeaveRequest> RepoOf(LeaveRequest request)
    {
        var requests = Substitute.For<IRepository<LeaveRequest>>();
        requests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>()).Returns(request);
        requests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>()).Returns(new List<LeaveRequest>());
        return requests;
    }

    private IReadRepository<Employee> Staff()
    {
        var employees = Substitute.For<IReadRepository<Employee>>();
        employees.GetByIdAsync(_staff.Id, Arg.Any<CancellationToken>()).Returns(_staff);
        return employees;
    }

    // ---- corrections after close (Q8–Q11) -----------------------------

    [Fact]
    public async Task Owner_cancelling_closed_leave_refunds_the_cut_day_into_the_first_open_month()
    {
        var request = ClosedIzin();

        var result = await CancelLeaveRequestHandler.Handle(
            new CancelLeaveRequestCommand(request.Id.Value, _ownerCaller, "Budi worked all three days"),
            RepoOf(request), Staff(), _policy, _clock, Substitute.For<IMessageBus>(), Substitute.For<IPayrollLock>(),
            Ledger(), CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
        request.CorrectionReason.Should().Be("Budi worked all three days");
        request.CorrectedMonth.Should().Be(March);

        // The paid lines stay as history; only the cut one moved money.
        _existing.Should().OnlyContain(l => !l.IsActive && l.SupersededByName == "Kevin");
        _existing.Single(l => l.CutDays > 0).SupersededTargetMonth.Should().Be(April);
        _existing.Where(l => l.CutDays == 0).Should().OnlyContain(l => l.SupersededTargetMonth == null);

        var refund = _moved.Should().ContainSingle().Subject;
        (refund.TargetMonth, refund.SourceMonth, refund.Date).Should().Be((April, March, new LocalDate(2026, 3, 18)));
        refund.Amount.Should().Be(-250_000m);
    }

    [Fact]
    public async Task A_day_added_to_a_closed_month_is_priced_with_that_months_divisor()
    {
        var request = ClosedIzin();
        _marchDivisor = 25; // March closed at 25; whatever the company uses today doesn't matter

        var result = await EditLeaveRequestHandler.Handle(
            new EditLeaveRequestCommand(
                request.Id.Value, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 19), false, null, null, null,
                _ownerCaller, "Absent on the 19th too"),
            RepoOf(request), Substitute.For<IRepository<Employee>>().Also(r => r.GetByIdAsync(_staff.Id, Arg.Any<CancellationToken>()).Returns(_staff)),
            TestPayroll.Empty<IRepository<AttendanceDay>, AttendanceDay>(), ClosedMonths(), TestPayroll.NoLines(),
            TestOvertime.None(), _policy, TestPayroll.Empty<IReadRepository<LeaveRequest>, LeaveRequest>(), _clock, Substitute.For<IPayrollLock>(), Ledger(),
            CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();

        // 16–18 kept their labels; the 19th is new, cut, and charged in April at Rp 5.000.000 / 25.
        _existing.Should().OnlyContain(l => l.IsActive);
        var added = _added.Should().ContainSingle().Subject;
        (added.Date, added.CutDays, added.DailyRate, added.PaidAtClose, added.LateTargetMonth)
            .Should().Be((new LocalDate(2026, 3, 19), 1m, 200_000m, false, (LocalDate?)April));
        _moved.Should().ContainSingle().Which.Amount.Should().Be(200_000m);
    }

    [Fact]
    public async Task Approving_free_leave_into_a_closed_month_stamps_it_there()
    {
        var request = LeaveRequest.Create(
            _staff.Id, LeaveType.Permission, new LocalDate(2026, 3, 23), new LocalDate(2026, 3, 23), "izin", null,
            false, null, null, null, Guid.NewGuid(), Now, _policy);

        var result = await ApproveLeaveRequestHandler.Handle(
            new ApproveLeaveRequestCommand(request.Id.Value, _managerCaller),
            RepoOf(request), Staff(), ClosedMonths(), TestPayroll.NoLines(), _policy, _clock,
            Substitute.For<IMessageBus>(), Substitute.For<IPayrollLock>(), Ledger(), CancellationToken.None);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
        var stamped = _added.Should().ContainSingle().Subject;
        (stamped.Month, stamped.FreeDays, stamped.CutDays, stamped.PaidAtClose).Should().Be((March, 1m, 0m, false));
        _moved.Should().BeEmpty();
    }

    [Fact]
    public async Task The_preview_shows_the_refund_without_writing_anything()
    {
        var request = ClosedIzin();
        var read = Substitute.For<IReadRepository<LeaveRequest>>();
        read.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>()).Returns(request);
        read.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>()).Returns(new List<LeaveRequest>());

        var result = await PreviewLeaveCorrectionHandler.Handle(
            new PreviewLeaveCorrectionQuery(
                request.Id.Value, false, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 17), false, null, null, null, _ownerCaller),
            read, Staff(), _policy, _clock, Ledger(read), CancellationToken.None);

        var preview = result.Should().BeOfType<Result<LeaveCorrectionPreviewResult>.Success>().Subject.Value;
        (preview.OverQuotaDaysBefore, preview.OverQuotaDaysAfter).Should().Be((1m, 0m));
        preview.TargetMonth.Should().Be(April.ToDateOnly());
        preview.NetAmount.Should().Be(-250_000m);
        _existing.Should().OnlyContain(l => l.IsActive);
        await _lines.DidNotReceiveWithAnyArgs().UpdateRangeAsync(default!, default);
        _moved.Should().BeEmpty();
    }

    // ---- payroll state on a request (Q3/Q4) ----------------------------

    [Fact]
    public void A_closed_month_disables_edit_for_the_manager_and_makes_it_a_correction_for_the_owner()
    {
        var request = ClosedIzin();

        var rina = LeaveRequestResult.PayrollStateFor(_managerCaller, request, _staff, March);
        var kevin = LeaveRequestResult.PayrollStateFor(_ownerCaller, request, _staff, March);

        (rina.EditBlocked, rina.CancelBlocked, rina.IsCorrection).Should().Be((true, true, false));
        (kevin.EditBlocked, kevin.CancelBlocked, kevin.IsCorrection).Should().Be((false, false, true));
        LeaveRequestResult.PermissionsFor(_managerCaller, request, _staff, March)
            .Should().Be((false, false, false));
    }

    // ---- adjustments (Q12/Q15) ------------------------------------------

    private async Task<Result<LeaveDeductionAdjustmentResult>> AdjustAsync(
        LocalDate month, Employee employee, decimal amount, Caller? caller = null, string reason = "Refund 20 Mar")
    {
        var settings = Substitute.For<IReadRepository<PayrollSettings>>();
        settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>()).Returns(new PayrollSettings(January));
        var employees = Substitute.For<IReadRepository<Employee>>();
        employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        return await AddLeaveDeductionAdjustmentHandler.Handle(
            new AddLeaveDeductionAdjustmentCommand(month.ToDateOnly(), employee.Id.Value, amount, reason, caller ?? _ownerCaller),
            settings, ClosedMonths(), employees, Substitute.For<IRepository<LeaveDeductionAdjustment>>(), _clock,
            Substitute.For<IPayrollLock>(), CancellationToken.None);
    }

    [Fact]
    public async Task An_owner_adds_a_refund_to_an_open_month()
    {
        var result = await AdjustAsync(April, _staff, -250_000m);

        result.Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Success>()
            .Which.Value.Amount.Should().Be(-250_000m);
    }

    [Fact]
    public async Task Adjustments_are_refused_where_they_cannot_apply()
    {
        var gone = Person("Andi", EmployeeRole.Staff, _manager.Id, "3201234567890126");
        gone.Terminate(new LocalDate(2026, 3, 31));

        (await AdjustAsync(April, _staff, -1m, _managerCaller)).Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
        (await AdjustAsync(March, _staff, -1m)).Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Error>()
            .Which.Code.Should().Be("payroll.already_closed");
        (await AdjustAsync(April, _owner, -1m)).Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Error>()
            .Which.Code.Should().Be("payroll.adjustment_owner");
        (await AdjustAsync(April, gone, -1m)).Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Error>()
            .Which.Code.Should().Be("payroll.adjustment_terminated");
        (await AdjustAsync(April, _staff, 0m)).Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Error>()
            .Which.Code.Should().Be("payroll.adjustment_amount");
        (await AdjustAsync(April, _staff, 10m, reason: " ")).Should().BeOfType<Result<LeaveDeductionAdjustmentResult>.Error>()
            .Which.Code.Should().Be("payroll.adjustment_reason");
    }

    // ---- the month view (Q14/Q17) ---------------------------------------

    [Fact]
    public async Task A_month_with_more_refunds_than_cuts_rounds_toward_zero_and_counts_as_a_refund()
    {
        var settings = Substitute.For<IReadRepository<PayrollSettings>>();
        settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>()).Returns(new PayrollSettings(January));
        var corrections = TestPayroll.Empty<IReadRepository<LeaveDeductionCorrection>, LeaveDeductionCorrection>();
        corrections.ListAsync(Arg.Any<ISpecification<LeaveDeductionCorrection>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionCorrection>
            {
                new(April, March, _staff.Id.Value, Guid.NewGuid(), new LocalDate(2026, 3, 18), LeaveType.Annual,
                    -1m, 227_777.75m, "worked", Guid.NewGuid(), "Kevin", Now),
            });
        var adjustments = TestPayroll.Empty<IReadRepository<LeaveDeductionAdjustment>, LeaveDeductionAdjustment>();
        adjustments.ListAsync(Arg.Any<ISpecification<LeaveDeductionAdjustment>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionAdjustment> { new(April, _staff.Id.Value, 100_000m, "missed afternoon", Guid.NewGuid(), "Kevin", Now) });
        var employees = TestPayroll.Empty<IReadRepository<Employee>, Employee>();
        employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>()).Returns(new List<Employee> { _staff });

        var read = new LeaveDeductionReaders(
            ClosedMonths(), TestPayroll.NoLines(), corrections, adjustments,
            TestPayroll.Empty<IReadRepository<PayrollSettingsChange>, PayrollSettingsChange>(),
            TestPayroll.Empty<IReadRepository<LeaveRequest>, LeaveRequest>(), employees,
            TestPayroll.Empty<IReadRepository<EmployeeSalaryHistory>, EmployeeSalaryHistory>());

        var result = await GetLeaveDeductionMonthHandler.Handle(
            new GetLeaveDeductionMonthQuery(April.ToDateOnly(), _ownerCaller), settings, read, _policy, _clock, CancellationToken.None);

        // −227.777,75 + 100.000 = −127.777,75 → a refund of Rp 127.000.
        var month = result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Success>().Subject.Value;
        var budi = month.Rows.Should().ContainSingle().Subject;
        budi.Total.Should().Be(-127_000m);
        budi.Corrections.Should().ContainSingle();
        budi.Adjustments.Should().ContainSingle();
        (month.CutsTotal, month.RefundsTotal).Should().Be((0m, 127_000m));
        month.CloseBlockers.Should().ContainSingle().Which.Code.Should().Be("not_ended");
    }

    [Fact]
    public void Close_blockers_name_every_reason()
    {
        var first = January;
        LeaveDeductionMonths.CloseBlockersFor(new LocalDate(2025, 12, 1), new LocalDate(2025, 12, 31), new LocalDate(2026, 4, 10), first, [], 0)
            .Select(b => b.Code).Should().Equal("before_launch");
        LeaveDeductionMonths.CloseBlockersFor(March, LeaveDeductionMonth.EndOf(March), new LocalDate(2026, 3, 20), first, [January], 2)
            .Select(b => b.Code).Should().Equal("not_ended", "earlier_open", "pending");
        LeaveDeductionMonths.CloseBlockersFor(March, LeaveDeductionMonth.EndOf(March), new LocalDate(2026, 4, 10), first, [January], 0)
            .Should().ContainSingle().Which.Month.Should().Be(new DateOnly(2026, 2, 1));
    }

    // ---- divisor history (Q5) and exception audit -----------------------

    [Fact]
    public async Task Changing_the_divisor_records_who_moved_it_from_what()
    {
        var settings = Substitute.For<IRepository<PayrollSettings>>();
        settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>()).Returns(new PayrollSettings(January));
        var changes = Substitute.For<IRepository<PayrollSettingsChange>>();

        await SetPayrollDivisorHandler.Handle(new SetPayrollDivisorCommand(22, _ownerCaller), settings, changes, _clock,
            Substitute.For<ILogger<SetPayrollDivisorCommand>>(), CancellationToken.None);
        await SetPayrollDivisorHandler.Handle(new SetPayrollDivisorCommand(22, _ownerCaller), settings, changes, _clock,
            Substitute.For<ILogger<SetPayrollDivisorCommand>>(), CancellationToken.None);

        await changes.Received(1).AddAsync(
            Arg.Is<PayrollSettingsChange>(c => c.OldDivisor == 20 && c.NewDivisor == 22 && c.ChangedByName == "Kevin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Changing_an_exception_raises_an_audit_event_and_an_unchanged_one_does_not()
    {
        _staff.ClearDomainEvents();

        _staff.SetLeaveDeductionException(0m, null);
        _staff.SetLeaveDeductionException(0m, null);

        _staff.DomainEvents.OfType<EmployeeLeaveDeductionExceptionChanged>().Should().ContainSingle()
            .Which.Should().Match<EmployeeLeaveDeductionExceptionChanged>(e =>
                e.OldFlatAmountPerDay == null && e.NewFlatAmountPerDay == 0m);
    }
}

internal static class SubstituteExtensions
{
    /// <summary>Configure a substitute inline: <c>Substitute.For&lt;T&gt;().Also(s =&gt; ...)</c>.</summary>
    internal static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
