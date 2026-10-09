using Ardalis.Specification;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Leave;
using Erp.UseCases.Payroll.Common;
using NSubstitute;

namespace Erp.UnitTests;

internal static class TestPayroll
{
    /// <summary>No payroll month has been closed — the world every pre-GSS03 leave test lives in.</summary>
    internal static IReadRepository<LeaveDeductionMonth> NoMonths()
    {
        var repository = Substitute.For<IReadRepository<LeaveDeductionMonth>>();
        repository.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionMonth>());
        return repository;
    }

    /// <summary>A repository holding exactly the given closed months.</summary>
    internal static IReadRepository<LeaveDeductionMonth> Closed(params NodaTime.LocalDate[] months)
    {
        var repository = Substitute.For<IReadRepository<LeaveDeductionMonth>>();
        repository.ListAsync(Arg.Any<ISpecification<LeaveDeductionMonth>>(), Arg.Any<CancellationToken>())
            .Returns(months.Select(m => new LeaveDeductionMonth(m, PayrollSettings.DefaultDivisor, Guid.NewGuid(), "Owner", NodaTime.Instant.FromUtc(2026, 1, 1, 0, 0))).ToList());
        return repository;
    }

    internal static IReadRepository<LeaveDeductionLine> NoLines()
    {
        var repository = Substitute.For<IReadRepository<LeaveDeductionLine>>();
        repository.ListAsync(Arg.Any<ISpecification<LeaveDeductionLine>>(), Arg.Any<CancellationToken>())
            .Returns(new List<LeaveDeductionLine>());
        return repository;
    }

    /// <summary>An empty repository of <typeparamref name="T"/> that answers every list with nothing.</summary>
    internal static TRepo Empty<TRepo, T>()
        where TRepo : class, Ardalis.Specification.IReadRepositoryBase<T>
        where T : class
    {
        var repository = Substitute.For<TRepo>();
        repository.ListAsync(Arg.Any<ISpecification<T>>(), Arg.Any<CancellationToken>()).Returns(new List<T>());
        return repository;
    }

    /// <summary>
    /// The closed-month ledger over the given repositories; anything left out is empty. Pass the same
    /// <paramref name="months"/> the handler under test sees.
    /// </summary>
    internal static ClosedMonthLedger Ledger(
        IReadRepository<LeaveDeductionMonth>? months = null,
        IRepository<LeaveDeductionLine>? lines = null,
        IRepository<LeaveDeductionCorrection>? corrections = null,
        IReadRepository<LeaveRequest>? leaveRequests = null,
        IReadRepository<LeaveDeductionMonthException>? exceptions = null,
        IReadRepository<EmployeeSalaryHistory>? salaries = null,
        AttendanceDayPolicy? policy = null) =>
        new(
            lines ?? Empty<IRepository<LeaveDeductionLine>, LeaveDeductionLine>(),
            months ?? NoMonths(),
            exceptions ?? Empty<IReadRepository<LeaveDeductionMonthException>, LeaveDeductionMonthException>(),
            corrections ?? Empty<IRepository<LeaveDeductionCorrection>, LeaveDeductionCorrection>(),
            leaveRequests ?? Empty<IReadRepository<LeaveRequest>, LeaveRequest>(),
            salaries ?? Empty<IReadRepository<EmployeeSalaryHistory>, EmployeeSalaryHistory>(),
            policy ?? TestPolicies.Standard);
}
