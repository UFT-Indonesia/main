using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Employees.Events;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.Infrastructure.DomainEventHandlers;
using Erp.SharedKernel.Identity;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using Erp.UseCases.Attendance.Holidays.RemoveHoliday;
using Erp.UseCases.Attendance.Holidays.SaveHoliday;
using Erp.UseCases.Common;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.SetLeaveDeductionException;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.DecideLeaveRequest;
using Erp.UseCases.Leave.EditLeaveRequest;
using Erp.UseCases.Payroll;
using Erp.UseCases.Payroll.Common;
using FluentAssertions;
using NodaTime;
using NSubstitute;
using Wolverine;

namespace Erp.UnitTests.UseCases;

/// <summary>GSS03: the Potongan Cuti month, its locks, and the salary history it prices from.</summary>
public class PayrollHandlersTests
{
    // Wed 1 Apr 2026: March has ended, and the launch month (Jan) is before it.
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 3, 0);
    private static readonly LocalDate March = new(2026, 3, 1);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IPayrollLock _lock = Substitute.For<IPayrollLock>();
    private readonly AttendanceDayPolicy _policy = TestPolicies.Standard;

    private readonly Employee _owner;
    private readonly Employee _manager;
    private readonly Employee _staff;
    private readonly Caller _ownerCaller;
    private readonly Caller _managerCaller;

    private readonly IReadRepository<PayrollSettings> _settings = Substitute.For<IReadRepository<PayrollSettings>>();
    private readonly IRepository<LeaveDeductionMonth> _months = Substitute.For<IRepository<LeaveDeductionMonth>>();
    private readonly IRepository<LeaveDeductionLine> _lines = Substitute.For<IRepository<LeaveDeductionLine>>();
    private readonly IReadRepository<LeaveDeductionMonth> _monthsRead = Substitute.For<IReadRepository<LeaveDeductionMonth>>();
    private readonly IReadRepository<LeaveDeductionLine> _linesRead = Substitute.For<IReadRepository<LeaveDeductionLine>>();
    private readonly IReadRepository<LeaveRequest> _leave = Substitute.For<IReadRepository<LeaveRequest>>();
    private readonly IReadRepository<Employee> _employees = Substitute.For<IReadRepository<Employee>>();
    private readonly IReadRepository<EmployeeSalaryHistory> _salaries = Substitute.For<IReadRepository<EmployeeSalaryHistory>>();
    private readonly IRepository<LeaveDeductionMonthException> _monthExceptions = Substitute.For<IRepository<LeaveDeductionMonthException>>();
    private readonly List<LeaveDeductionLine> _written = [];
    private readonly LeaveDeductionReaders _read;

    public PayrollHandlersTests()
    {
        _clock.GetCurrentInstant().Returns(Now);

        _owner = Person("Owner Utama", EmployeeRole.Owner, null, "3201234567890123");
        _manager = Person("Manager Satu", EmployeeRole.Manager, _owner.Id, "3201234567890124");
        _staff = Person("Budi", EmployeeRole.Staff, _manager.Id, "3201234567890125");
        _ownerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Owner, _owner.Id, "Owner Utama");
        _managerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Manager, _manager.Id, "Manager Satu");

        _settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(new PayrollSettings(new LocalDate(2026, 1, 1)));
        _employees.ListAsync(Arg.Any<ISpecification<Employee>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Employee> { _staff });
        _salaries.ListAsync(Arg.Any<ISpecification<EmployeeSalaryHistory>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeSalaryHistory>());
        _linesRead.ListAsync(Arg.Any<ISpecification<LeaveDeductionLine>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionLine>());
        _leave.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest>());
        _monthsRead.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionMonth>());

        _read = new LeaveDeductionReaders(
            _monthsRead, _linesRead,
            TestPayroll.Empty<IReadRepository<LeaveDeductionCorrection>, LeaveDeductionCorrection>(),
            TestPayroll.Empty<IReadRepository<LeaveDeductionAdjustment>, LeaveDeductionAdjustment>(),
            TestPayroll.Empty<IReadRepository<PayrollSettingsChange>, PayrollSettingsChange>(),
            _leave, _employees, _salaries);

        _lines.AddRangeAsync(Arg.Do<IEnumerable<LeaveDeductionLine>>(lines => _written.AddRange(lines)), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<LeaveDeductionLine>>());
    }

    private static Employee Person(string name, EmployeeRole role, EmployeeId? parentId, string nik) =>
        Employee.Create(name, Nik.Create(nik), Money.Idr(5_000_000m), new LocalDate(2026, 1, 1), role, parentId);

    private LeaveRequest Approved(Employee subject, LocalDate start, LocalDate end, Instant at)
    {
        var request = LeaveRequest.Create(
            subject.Id, LeaveType.Annual, start, end, "cuti", null, false, null, null, null, Guid.NewGuid(), at, _policy);
        request.Approve(Guid.NewGuid(), "Manager Satu", at);
        return request;
    }

    /// <summary>Budi's year from the GSS03 worked example: a 10-day trip approved first, then 4 days in March.</summary>
    private void WorkedExample()
    {
        var trip = Approved(_staff, new LocalDate(2026, 11, 2), new LocalDate(2026, 11, 13), Instant.FromUtc(2026, 1, 15, 3, 0));
        var march = Approved(_staff, new LocalDate(2026, 3, 9), new LocalDate(2026, 3, 12), Instant.FromUtc(2026, 3, 1, 3, 0));
        _leave.ListAsync(Arg.Is<ISpecification<LeaveRequest>>(s => s is ApprovedLeaveInYearSpec), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest> { trip, march });
        _leave.ListAsync(Arg.Is<ISpecification<LeaveRequest>>(s => s is LeaveRequestsByIdsSpec), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest> { trip, march });
    }

    private Task<Result<LeaveDeductionMonthResult>> CloseAsync(LocalDate month, Caller? caller = null) =>
        CloseLeaveDeductionMonthHandler.Handle(
            new CloseLeaveDeductionMonthCommand(month.ToDateOnly(), caller ?? _ownerCaller),
            _settings, _months, _lines, _monthExceptions, _read, _policy, _clock, _lock, CancellationToken.None);

    // ---- the page -------------------------------------------------------

    [Fact]
    public async Task March_shows_the_two_cut_days_at_the_daily_rate()
    {
        WorkedExample();

        var result = await GetLeaveDeductionMonthHandler.Handle(
            new GetLeaveDeductionMonthQuery(March.ToDateOnly(), _ownerCaller),
            _settings, _read, _policy, _clock, CancellationToken.None);

        var month = result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Success>().Subject.Value;
        var budi = month.Rows.Should().ContainSingle().Subject;
        budi.CutDays.Should().Be(2);
        budi.Total.Should().Be(500_000m);
        budi.Days.Where(d => d.CutDays > 0).Select(d => d.Date.Day).Should().Equal(11, 12);
        budi.Days.Select(d => d.DailyRate).Distinct().Should().Equal(250_000m);
    }

    [Fact]
    public async Task Only_an_owner_sees_the_page()
    {
        var result = await GetLeaveDeductionMonthHandler.Handle(
            new GetLeaveDeductionMonthQuery(March.ToDateOnly(), _managerCaller),
            _settings, _read, _policy, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task A_custom_divisor_exception_changes_the_rate()
    {
        WorkedExample();
        _staff.SetLeaveDeductionException(null, 25);

        var result = await GetLeaveDeductionMonthHandler.Handle(
            new GetLeaveDeductionMonthQuery(March.ToDateOnly(), _ownerCaller),
            _settings, _read, _policy, _clock, CancellationToken.None);

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Success>()
            .Which.Value.Rows.Single().Total.Should().Be(400_000m);
    }

    // ---- close ----------------------------------------------------------

    [Fact]
    public async Task Closing_stamps_every_leave_day_and_writes_the_month()
    {
        WorkedExample();
        _settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(new PayrollSettings(March));

        var result = await CloseAsync(March);

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Success>();
        _written.Should().HaveCount(4);
        _written.Where(l => l.CutDays > 0).Select(l => l.Date.Day).Should().Equal(11, 12);
        _written.Should().OnlyContain(l => l.Salary == 5_000_000m && l.DailyRate == 250_000m);
        await _months.Received(1).AddAsync(
            Arg.Is<LeaveDeductionMonth>(m => m.Month == March && m.Divisor == 20), Arg.Any<CancellationToken>());
        _written.Should().OnlyContain(l => l.PaidAtClose);
    }

    [Fact]
    public async Task Closing_records_each_employees_exception_as_it_stands()
    {
        WorkedExample();
        _settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(new PayrollSettings(March));
        _staff.SetLeaveDeductionException(null, 25);

        await CloseAsync(March);

        await _monthExceptions.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<LeaveDeductionMonthException>>(rows =>
                rows.Single().EmployeeId == _staff.Id.Value && rows.Single().Divisor == 25 && rows.Single().Month == March),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_month_cannot_be_closed_before_it_ends()
    {
        var result = await CloseAsync(new LocalDate(2026, 4, 1));

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>().Which.Code.Should().Be("payroll.not_ended");
    }

    [Fact]
    public async Task Months_close_oldest_first()
    {
        var result = await CloseAsync(March); // January and February are still open, and January is the launch month

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>().Which.Code.Should().Be("payroll.close_in_order");
    }

    [Fact]
    public async Task A_month_before_launch_cannot_be_closed()
    {
        _settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(new PayrollSettings(new LocalDate(2026, 3, 1)));

        var result = await CloseAsync(new LocalDate(2026, 2, 1));

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>().Which.Code.Should().Be("payroll.before_first_month");
    }

    [Fact]
    public async Task A_closed_month_cannot_be_closed_again()
    {
        _monthsRead.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionMonth> { new(March, 20, Guid.NewGuid(), "Owner", Now) });

        var result = await CloseAsync(March);

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>().Which.Code.Should().Be("payroll.already_closed");
    }

    [Fact]
    public async Task Pending_leave_in_the_month_blocks_the_close()
    {
        _settings.GetByIdAsync(PayrollSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(new PayrollSettings(March));
        var pending = LeaveRequest.Create(
            _staff.Id, LeaveType.Annual, new LocalDate(2026, 3, 16), new LocalDate(2026, 3, 17), "cuti", null,
            false, null, null, null, Guid.NewGuid(), Now, _policy);
        typeof(LeaveRequest).GetProperty(nameof(LeaveRequest.Employee))!.SetValue(pending, _staff);
        _leave.ListAsync(Arg.Is<ISpecification<LeaveRequest>>(s => s is PendingLeaveOverlappingSpec), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest> { pending });

        var result = await CloseAsync(March);

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>().Which.Code.Should().Be("payroll.pending_leave");
    }

    [Fact]
    public async Task Only_an_owner_closes()
    {
        var result = await CloseAsync(March, _managerCaller);

        result.Should().BeOfType<Result<LeaveDeductionMonthResult>.Error>().Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    // ---- locks ----------------------------------------------------------

    [Fact]
    public async Task Only_the_owner_may_change_approved_leave_in_a_closed_month_and_only_with_a_reason()
    {
        var request = Approved(_staff, new LocalDate(2026, 3, 9), new LocalDate(2026, 3, 10), Instant.FromUtc(2026, 3, 1, 3, 0));
        var requests = Substitute.For<IRepository<LeaveRequest>>();
        requests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>()).Returns(request);
        var staffRepo = Substitute.For<IRepository<Employee>>();
        staffRepo.GetByIdAsync(_staff.Id, Arg.Any<CancellationToken>()).Returns(_staff);
        var closed = TestPayroll.Closed(March);

        var employees = Substitute.For<IReadRepository<Employee>>();
        employees.GetByIdAsync(_staff.Id, Arg.Any<CancellationToken>()).Returns(_staff);

        Task<Result<LeaveRequestResult>> Edit(Caller caller, string? reason) => EditLeaveRequestHandler.Handle(
            new EditLeaveRequestCommand(
                request.Id.Value, new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 11), false, null, null, null, caller, reason),
            requests, staffRepo, Substitute.For<IRepository<AttendanceDay>>(), closed, TestPayroll.NoLines(),
            TestOvertime.None(), _policy, Substitute.For<IReadRepository<LeaveRequest>>(), _clock,
            Substitute.For<IPayrollLock>(), TestPayroll.Ledger(closed), CancellationToken.None);

        Task<Result<LeaveRequestResult>> Cancel(Caller caller, string? note) => CancelLeaveRequestHandler.Handle(
            new CancelLeaveRequestCommand(request.Id.Value, caller, note),
            requests, employees, _policy, _clock, Substitute.For<IMessageBus>(), Substitute.For<IPayrollLock>(),
            TestPayroll.Ledger(closed), CancellationToken.None);

        (await Edit(_managerCaller, "typo")).Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.payroll_closed");
        (await Cancel(_managerCaller, "typo")).Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.payroll_closed");
        (await Edit(_ownerCaller, null)).Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.correction_reason");
        (await Cancel(_ownerCaller, " ")).Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.correction_reason");
        request.Status.Should().Be(LeaveRequestStatus.Approved);
    }

    [Fact]
    public async Task A_holiday_cannot_be_declared_or_removed_in_a_closed_month()
    {
        var holidays = Substitute.For<IRepository<Holiday>>();
        var closed = TestPayroll.Closed(March);

        var declare = await SaveHolidayHandler.Handle(
            new SaveHolidayCommand(new DateOnly(2026, 3, 20), "Nyepi", "National", Guid.NewGuid()),
            holidays, closed, _clock, Substitute.For<IMessageBus>(), Substitute.For<IPayrollLock>(), CancellationToken.None);

        var existing = Holiday.Declare(new LocalDate(2026, 3, 20), "Nyepi", HolidayKind.National, Guid.NewGuid(), Now);
        holidays.FirstOrDefaultAsync(Arg.Any<ISpecification<Holiday>>(), Arg.Any<CancellationToken>()).Returns(existing);
        var remove = await RemoveHolidayHandler.Handle(
            new RemoveHolidayCommand(new DateOnly(2026, 3, 20)), holidays, closed,
            Substitute.For<IMessageBus>(), Substitute.For<IPayrollLock>(), CancellationToken.None);

        declare.Should().BeOfType<Result<HolidayResult>.Error>().Which.Code.Should().Be("holiday.payroll_closed");
        remove.Should().BeOfType<Result<bool>.Error>().Which.Code.Should().Be("holiday.payroll_closed");
    }

    // ---- salary history -------------------------------------------------

    [Fact]
    public async Task First_salary_change_of_a_new_hire_writes_the_old_salary_too()
    {
        var salaries = Substitute.For<IRepository<EmployeeSalaryHistory>>();
        salaries.ListAsync(Arg.Any<ISpecification<EmployeeSalaryHistory>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeSalaryHistory>());
        var added = new List<EmployeeSalaryHistory>();
        salaries.AddAsync(Arg.Do<EmployeeSalaryHistory>(added.Add), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<EmployeeSalaryHistory>());
        var id = Guid.NewGuid();

        await EmployeeSalaryChangedHandler.Handle(
            new EmployeeSalaryChanged(id, Money.Idr(5_000_000m), new LocalDate(2026, 1, 1), Money.Idr(6_000_000m), new LocalDate(2026, 4, 1)),
            Substitute.For<IRepository<EmployeeAuditLog>>(), salaries, new Envelope(), CancellationToken.None);

        added.Select(h => (h.EffectiveFrom, h.Amount)).Should().Equal(
            (new LocalDate(2026, 1, 1), 5_000_000m), (new LocalDate(2026, 4, 1), 6_000_000m));
    }

    [Fact]
    public async Task A_same_date_correction_replaces_the_row()
    {
        var id = Guid.NewGuid();
        var row = new EmployeeSalaryHistory(id, new LocalDate(2026, 4, 1), 6_000_000m);
        var salaries = Substitute.For<IRepository<EmployeeSalaryHistory>>();
        salaries.ListAsync(Arg.Any<ISpecification<EmployeeSalaryHistory>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmployeeSalaryHistory> { new(id, new LocalDate(2026, 1, 1), 5_000_000m), row });

        await EmployeeSalaryChangedHandler.Handle(
            new EmployeeSalaryChanged(id, Money.Idr(6_000_000m), new LocalDate(2026, 4, 1), Money.Idr(6_500_000m), new LocalDate(2026, 4, 1)),
            Substitute.For<IRepository<EmployeeAuditLog>>(), salaries, new Envelope(), CancellationToken.None);

        row.Amount.Should().Be(6_500_000m);
        await salaries.DidNotReceive().AddAsync(Arg.Any<EmployeeSalaryHistory>(), Arg.Any<CancellationToken>());
        await salaries.Received(1).UpdateAsync(row, Arg.Any<CancellationToken>());
    }

    // ---- per-employee exception ----------------------------------------

    [Fact]
    public async Task Only_an_owner_sets_the_exception()
    {
        var employees = Substitute.For<IRepository<Employee>>();
        employees.GetByIdAsync(_staff.Id, Arg.Any<CancellationToken>()).Returns(_staff);

        var asManager = await SetLeaveDeductionExceptionHandler.Handle(
            new SetLeaveDeductionExceptionCommand(_staff.Id.Value, 0m, null, _managerCaller), employees, Substitute.For<IMessageBus>(), CancellationToken.None);
        var asOwner = await SetLeaveDeductionExceptionHandler.Handle(
            new SetLeaveDeductionExceptionCommand(_staff.Id.Value, 0m, null, _ownerCaller), employees, Substitute.For<IMessageBus>(), CancellationToken.None);

        asManager.Should().BeOfType<Result<EmployeeResult>.Error>().Which.Code.Should().Be(ResultErrors.Forbidden);
        asOwner.Should().BeOfType<Result<EmployeeResult>.Success>()
            .Which.Value.LeaveDeductionFlatAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Closing_takes_the_exclusive_payroll_lock_but_only_for_an_owner()
    {
        await CloseAsync(new LocalDate(2026, 3, 1), _managerCaller);
        await _lock.DidNotReceive().AcquireExclusiveAsync(Arg.Any<CancellationToken>());

        await CloseAsync(new LocalDate(2026, 3, 1));
        await _lock.Received(1).AcquireExclusiveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Holiday_changes_take_the_shared_payroll_lock()
    {
        var payrollLock = Substitute.For<IPayrollLock>();
        var holidays = Substitute.For<IRepository<Holiday>>();

        await SaveHolidayHandler.Handle(
            new SaveHolidayCommand(new DateOnly(2026, 3, 20), "Nyepi", "National", Guid.NewGuid()),
            holidays, TestPayroll.NoMonths(), _clock, Substitute.For<IMessageBus>(), payrollLock, CancellationToken.None);
        await RemoveHolidayHandler.Handle(
            new RemoveHolidayCommand(new DateOnly(2026, 3, 20)), holidays, TestPayroll.NoMonths(),
            Substitute.For<IMessageBus>(), payrollLock, CancellationToken.None);

        await payrollLock.Received(2).AcquireSharedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_flat_amount_and_a_divisor_together_are_refused()
    {
        var employees = Substitute.For<IRepository<Employee>>();
        employees.GetByIdAsync(_staff.Id, Arg.Any<CancellationToken>()).Returns(_staff);

        var result = await SetLeaveDeductionExceptionHandler.Handle(
            new SetLeaveDeductionExceptionCommand(_staff.Id.Value, 1m, 20, _ownerCaller), employees, Substitute.For<IMessageBus>(), CancellationToken.None);

        result.Should().BeOfType<Result<EmployeeResult>.Error>().Which.Code.Should().Be("employee.deduction_exception");
    }
}
