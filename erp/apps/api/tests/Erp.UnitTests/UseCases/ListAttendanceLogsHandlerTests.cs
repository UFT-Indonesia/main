using System.Reflection;
using Ardalis.Specification;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Common;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.ListAttendanceLogs;
using FluentAssertions;
using NodaTime;
using NSubstitute;

namespace Erp.UnitTests.UseCases;

public class ListAttendanceLogsHandlerTests
{
    private readonly IReadRepository<AttendanceLog> _logs = Substitute.For<IReadRepository<AttendanceLog>>();

    private static AttendanceLog MakeLog(EmployeeId employeeId)
        => AttendanceLog.FromDevice(employeeId, SystemClock.Instance.GetCurrentInstant(), PunchType.In, "DEV-01");

    private static Employee MakeEmployee(string name = "Alice")
        => Employee.Create(name, Nik.Create("3201234567890123"), Money.Idr(5_000_000m), new LocalDate(2025, 1, 1), EmployeeRole.Owner);

    private static AttendanceLog WithEmployee(AttendanceLog log, Employee employee)
    {
        typeof(AttendanceLog)
            .GetProperty(nameof(AttendanceLog.Employee), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(log, employee);
        return log;
    }

    [Fact]
    public async Task Handle_returns_paged_results()
    {
        var emp = MakeEmployee();
        var log = WithEmployee(MakeLog(emp.Id), emp);

        _logs.CountAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(1);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog> { log });

        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(Page: 1, PageSize: 20, EmployeeSearch: null, DateFrom: null, DateTo: null, PunchType: null, Source: null),
            _logs, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListAttendanceLogsResult>.Success>().Subject;
        success.Value.Items.Should().HaveCount(1);
        success.Value.TotalCount.Should().Be(1);
        success.Value.Page.Should().Be(1);
        success.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_clamps_page_size_to_max()
    {
        _logs.CountAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(0);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>());

        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(1, 1000, null, null, null, null, null),
            _logs, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListAttendanceLogsResult>.Success>().Subject;
        success.Value.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Handle_defaults_page_and_page_size_when_zero()
    {
        _logs.CountAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(0);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>());

        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(0, 0, null, null, null, null, null),
            _logs, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListAttendanceLogsResult>.Success>().Subject;
        success.Value.Page.Should().Be(1);
        success.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_source()
    {
        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(1, 20, null, null, null, null, "Fax"),
            _logs, CancellationToken.None);

        result.Should().BeOfType<Result<ListAttendanceLogsResult>.Error>()
            .Which.Code.Should().Be("attendance.source_invalid");
    }

    [Fact]
    public async Task Handle_returns_error_for_invalid_punch_type()
    {
        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(1, 20, null, null, null, "Break", null),
            _logs, CancellationToken.None);

        result.Should().BeOfType<Result<ListAttendanceLogsResult>.Error>()
            .Which.Code.Should().Be("attendance.punch_type_invalid");
    }

    [Fact]
    public async Task Handle_returns_empty_when_employee_search_matches_nothing()
    {
        _logs.CountAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(0);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog>());

        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(1, 20, "ghost", null, null, null, null),
            _logs, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListAttendanceLogsResult>.Success>().Subject;
        success.Value.Items.Should().BeEmpty();
        success.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_maps_employee_name_onto_items()
    {
        var emp = MakeEmployee("Bob");
        var log = WithEmployee(MakeLog(emp.Id), emp);

        _logs.CountAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(1);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog> { log });

        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(1, 20, null, null, null, null, null),
            _logs, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListAttendanceLogsResult>.Success>().Subject;
        success.Value.Items.Single().EmployeeFullName.Should().Be("Bob");
    }

    [Fact]
    public async Task Handle_falls_back_to_dash_when_employee_not_in_batch()
    {
        var log = MakeLog(EmployeeId.New());

        _logs.CountAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(1);
        _logs.ListAsync(Arg.Any<ISpecification<AttendanceLog>>(), Arg.Any<CancellationToken>()).Returns(new List<AttendanceLog> { log });

        var result = await ListAttendanceLogsHandler.Handle(
            new ListAttendanceLogsQuery(1, 20, null, null, null, null, null),
            _logs, CancellationToken.None);

        var success = result.Should().BeOfType<Result<ListAttendanceLogsResult>.Success>().Subject;
        success.Value.Items.Single().EmployeeFullName.Should().Be("—");
    }
}
