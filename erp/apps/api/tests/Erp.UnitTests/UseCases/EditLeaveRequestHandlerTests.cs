using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;
using Erp.UseCases.Common;
using Erp.UseCases.Leave.Common;
using Erp.UseCases.Leave.EditLeaveRequest;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class EditLeaveRequestHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 7, 14, 8, 0);

    private readonly IRepository<LeaveRequest> _leaveRequests = Substitute.For<IRepository<LeaveRequest>>();
    private readonly IReadRepository<LeaveRequest> _leaveRequestsRead =
        Substitute.For<IReadRepository<LeaveRequest>>();
    private readonly IRepository<Employee> _employees = Substitute.For<IRepository<Employee>>();
    private readonly IRepository<AttendanceDay> _attendanceDays = Substitute.For<IRepository<AttendanceDay>>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly Employee _owner;
    private readonly Employee _manager;
    private readonly Employee _otherManager;
    private readonly Employee _staff;
    private readonly Employee _foreignStaff;

    private readonly Caller _ownerCaller;
    private readonly Caller _managerCaller;
    private readonly Caller _staffCaller;

    public EditLeaveRequestHandlerTests()
    {
        _clock.GetCurrentInstant().Returns(Now);

        _owner = NewEmployee("Owner Utama", EmployeeRole.Owner, null, "3201234567890123");
        _manager = NewEmployee("Manager Satu", EmployeeRole.Manager, _owner.Id, "3201234567890124");
        _otherManager = NewEmployee("Manager Dua", EmployeeRole.Manager, _owner.Id, "3201234567890127");
        _staff = NewEmployee("Staff Biasa", EmployeeRole.Staff, _manager.Id, "3201234567890125");
        _foreignStaff = NewEmployee("Staff Lain", EmployeeRole.Staff, _otherManager.Id, "3201234567890126");

        foreach (var employee in new[] { _owner, _manager, _otherManager, _staff, _foreignStaff })
        {
            _employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        }

        _ownerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Owner, _owner.Id, "Owner Utama");
        _managerCaller = new Caller(Guid.NewGuid(), EmployeeRole.Manager, _manager.Id, "Manager Satu");
        _staffCaller = new Caller(Guid.NewGuid(), EmployeeRole.Staff, _staff.Id, "Staff Biasa");

        // Nothing else approved, so overlap and quota both have a clean slate.
        _leaveRequests.ListAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest>());
        _attendanceDays.ListAsync(Arg.Any<ISpecification<AttendanceDay>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static Employee NewEmployee(string name, EmployeeRole role, EmployeeId? parentId, string nik) =>
        Employee.Create(
            name,
            Nik.Create(nik),
            Money.Idr(5_000_000m),
            new LocalDate(2026, 1, 1),
            role,
            parentId);

    /// <summary>Mon 3 – Fri 7 Aug 2026, Annual, filed by the subject themselves.</summary>
    private LeaveRequest Existing(Employee subject, bool approved, Guid? requestedByUserId = null)
    {
        var request = LeaveRequest.Create(
            subject.Id, LeaveType.Annual,
            new LocalDate(2026, 8, 3), new LocalDate(2026, 8, 7),
            "cuti", null, halfDay: false, halfDayPeriod: null, startHour: null, endHour: null,
            requestedByUserId ?? Guid.NewGuid(), Now);

        if (approved)
        {
            request.Approve(Guid.NewGuid(), "Owner Utama", Now);
        }

        _leaveRequests.FirstOrDefaultAsync(Arg.Any<ISpecification<LeaveRequest>>(), Arg.Any<CancellationToken>())
            .Returns(request);
        return request;
    }

    private Task<Result<LeaveRequestResult>> EditAsync(
        LeaveRequest request,
        Caller caller,
        DateOnly? start = null,
        DateOnly? end = null,
        bool halfDay = false,
        HalfDayPeriod? halfDayPeriod = null) =>
        EditLeaveRequestHandler.Handle(
            new EditLeaveRequestCommand(
                request.Id.Value,
                start ?? new DateOnly(2026, 8, 10),
                end ?? new DateOnly(2026, 8, 12),
                halfDay,
                halfDayPeriod,
                StartHour: null,
                EndHour: null,
                caller),
            _leaveRequests, _employees, _attendanceDays, TestPolicies.Standard,
            _leaveRequestsRead, _clock, CancellationToken.None);

    // ---- authority --------------------------------------------------------

    [Fact]
    public async Task An_owner_may_edit_anyones_leave()
    {
        var request = Existing(_staff, approved: true);

        var result = await EditAsync(request, _ownerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
        request.StartDate.Should().Be(new LocalDate(2026, 8, 10));
        request.EndDate.Should().Be(new LocalDate(2026, 8, 12));
    }

    [Fact]
    public async Task A_manager_may_edit_their_own_staffs_leave()
    {
        var request = Existing(_staff, approved: true);

        var result = await EditAsync(request, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Success>();
    }

    [Fact]
    public async Task A_manager_may_not_edit_leave_outside_their_line()
    {
        var request = Existing(_foreignStaff, approved: true);

        var result = await EditAsync(request, _managerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    [Fact]
    public async Task Staff_may_not_edit_their_own_leave()
    {
        var request = Existing(_staff, approved: true);

        var result = await EditAsync(request, _staffCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be(ResultErrors.Forbidden);
    }

    // ---- edit implies approval, for an Owner only -------------------------

    [Fact]
    public async Task An_owner_editing_a_pending_request_also_approves_it()
    {
        var request = Existing(_staff, approved: false);

        await EditAsync(request, _ownerCaller);

        request.Status.Should().Be(LeaveRequestStatus.Approved);
    }

    [Fact]
    public async Task A_manager_editing_a_pending_request_leaves_it_pending()
    {
        // The filer-cannot-decide rule bars this Manager from approving a request they filed;
        // an edit must not become a way around it.
        var request = Existing(_staff, approved: false, requestedByUserId: _managerCaller.UserId);

        await EditAsync(request, _managerCaller);

        request.Status.Should().Be(LeaveRequestStatus.Pending);
    }

    // ---- audit trail ------------------------------------------------------

    [Fact]
    public async Task An_edit_records_who_moved_it_and_the_dates_it_replaced()
    {
        var request = Existing(_staff, approved: true);

        await EditAsync(request, _ownerCaller);

        request.EditedByUserId.Should().Be(_ownerCaller.UserId);
        request.EditedByName.Should().Be("Owner Utama");
        request.EditedAtUtc.Should().Be(Now);
        request.PreviousStartDate.Should().Be(new LocalDate(2026, 8, 3));
        request.PreviousEndDate.Should().Be(new LocalDate(2026, 8, 7));
    }

    // ---- the same gates a new request faces -------------------------------

    [Fact]
    public async Task An_edit_that_overlaps_another_approved_leave_is_rejected()
    {
        var request = Existing(_staff, approved: true);

        var other = LeaveRequest.Create(
            _staff.Id, LeaveType.Sick,
            new LocalDate(2026, 8, 10), new LocalDate(2026, 8, 11),
            "sakit", TestAttachments.DoctorsNote(),
            halfDay: false, halfDayPeriod: null, startHour: null, endHour: null, Guid.NewGuid(), Now);
        other.Approve(Guid.NewGuid(), "Owner Utama", Now);
        _leaveRequests.ListAsync(Arg.Any<ApprovedLeaveOverlappingSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveRequest> { other });

        var result = await EditAsync(request, _ownerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.overlaps_approved");
    }

    [Fact]
    public async Task An_edit_to_a_range_with_no_workdays_is_rejected()
    {
        var request = Existing(_staff, approved: true);

        // Sat 8 – Sun 9 Aug 2026.
        var result = await EditAsync(
            request, _ownerCaller, new DateOnly(2026, 8, 8), new DateOnly(2026, 8, 9));

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.no_workdays");
    }

    [Fact]
    public async Task A_cancelled_request_cannot_be_edited()
    {
        var request = Existing(_staff, approved: true);
        request.Cancel(Guid.NewGuid(), "Owner Utama", Now, null, LeaveCancellationReason.WithdrawnByEmployee);

        var result = await EditAsync(request, _ownerCaller);

        result.Should().BeOfType<Result<LeaveRequestResult>.Error>()
            .Which.Code.Should().Be("leave.not_editable");
    }

    // ---- attendance re-sync -----------------------------------------------

    [Fact]
    public async Task Editing_an_approved_request_re_materializes_attendance_on_the_new_dates()
    {
        var request = Existing(_staff, approved: true);
        var oldRow = AttendanceDay.CreateForLeave(_staff.Id, new LocalDate(2026, 8, 3), request.Id);
        _attendanceDays.ListAsync(
            Arg.Any<AttendanceDaysForLeaveRequestSpec>(), Arg.Any<CancellationToken>())
            .Returns([oldRow]);

        var added = new List<AttendanceDay>();
        _ = _attendanceDays.AddAsync(Arg.Do<AttendanceDay>(added.Add), Arg.Any<CancellationToken>());

        await EditAsync(request, _ownerCaller);

        // The row the old dates put there is gone, and Mon 10 Aug has one instead.
        await _attendanceDays.Received(1).DeleteAsync(oldRow, Arg.Any<CancellationToken>());
        added.Select(day => day.CalendarDate).Should().Equal(new LocalDate(2026, 8, 10));
    }

    [Fact]
    public async Task Editing_a_pending_request_by_a_manager_touches_no_attendance()
    {
        var request = Existing(_staff, approved: false, requestedByUserId: _managerCaller.UserId);
        var added = new List<AttendanceDay>();
        _ = _attendanceDays.AddAsync(Arg.Do<AttendanceDay>(added.Add), Arg.Any<CancellationToken>());

        await EditAsync(request, _managerCaller);

        // Still Pending, so nothing was ever materialized to move.
        added.Should().BeEmpty();
        await _attendanceDays.DidNotReceive().DeleteAsync(
            Arg.Any<AttendanceDay>(), Arg.Any<CancellationToken>());
    }
}
