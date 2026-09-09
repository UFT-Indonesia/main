using System.Linq.Expressions;
using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Employees;
using Erp.Core.Aggregates.Leave;
using Erp.Core.Aggregates.Probation;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.ListAttendanceDays;
using Erp.UseCases.Common;
using Erp.UseCases.Common.Filtering;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.ListEmployees;
using Erp.UseCases.Leave.ListLeaveRequests;
using Erp.UseCases.Probation.ListProbationExtensionRequests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Erp.UnitTests.UseCases;

/// <summary>
/// Drives every registry field against every operator it allows and asks EF to turn the result
/// into SQL.
///
/// This is the test the filter builder cannot ship without. A registry entry is a lambda, so an
/// untranslatable one — a computed property with no column, a member of a value object EF stores
/// through a converter — compiles cleanly and throws only when a user happens to pick that field
/// in the UI. Translation happens when the query is built, not when it runs, so no database is
/// needed here: the provider is pointed at a connection string it never opens.
/// </summary>
public class FilterTranslationTests
{
    private static readonly Caller Owner =
        new(Guid.NewGuid(), EmployeeRole.Owner, new EmployeeId(Guid.NewGuid()), "Owner");

    public static TheoryData<string, string> EmployeeCases => Cases(EmployeeFilterFields.Fields);
    public static TheoryData<string, string> AttendanceDayCases => Cases(AttendanceDayFilterFields.Fields);
    public static TheoryData<string, string> AuditLogCases => Cases(EmployeeAuditLogFilterFields.Fields);
    public static TheoryData<string, string> LeaveCases => Cases(LeaveRequestFilterFields.Fields);
    public static TheoryData<string, string> ProbationCases => Cases(ProbationExtensionFilterFields.Fields);

    [Theory]
    [MemberData(nameof(EmployeeCases))]
    public void Employee_fields_translate(string field, string op)
        => Translates(EmployeeFilterFields.Fields, field, op, db => db.Employees);

    [Theory]
    [MemberData(nameof(AttendanceDayCases))]
    public void AttendanceDay_fields_translate(string field, string op)
        => Translates(AttendanceDayFilterFields.Fields, field, op, db => db.AttendanceDays);

    [Theory]
    [MemberData(nameof(AuditLogCases))]
    public void AuditLog_fields_translate(string field, string op)
        => Translates(EmployeeAuditLogFilterFields.Fields, field, op, db => db.EmployeeAuditLogs);

    [Theory]
    [MemberData(nameof(LeaveCases))]
    public void Leave_fields_translate(string field, string op)
        => Translates(LeaveRequestFilterFields.Fields, field, op, db => db.LeaveRequests);

    [Theory]
    [MemberData(nameof(ProbationCases))]
    public void Probation_fields_translate(string field, string op)
        => Translates(ProbationExtensionFilterFields.Fields, field, op, db => db.ProbationExtensionRequests);

    private static TheoryData<string, string> Cases<T>(FilterFieldMap<T> fields)
    {
        var data = new TheoryData<string, string>();
        foreach (var key in fields.Keys)
        {
            fields.TryGet(key, out var field);
            foreach (var op in field.AllowedOps)
            {
                data.Add(key, op);
            }
        }

        return data;
    }

    private static void Translates<T>(
        FilterFieldMap<T> fields,
        string field,
        string op,
        Func<AppDbContext, IQueryable<T>> source)
        where T : class
    {
        fields.TryGet(field, out var descriptor);
        var row = new FilterRow(field, op, descriptor.SampleValue(op));

        FilterApplier.TryCompile(fields, [row], Owner, out var predicates, out var failure)
            .Should().BeTrue($"'{field}' should accept '{op}' with its own sample value, but: {failure.Message}");

        using var db = ConnectionlessContext();
        var query = source(db);
        foreach (var predicate in predicates)
        {
            query = query.Where(predicate);
        }

        // Throws InvalidOperationException("could not be translated") if the lambda has no SQL.
        var act = () => query.ToQueryString();
        act.Should().NotThrow($"filtering '{field}' by '{op}' must translate to SQL");
    }

    /// <summary>
    /// The real model and provider, pointed at a database that is never contacted — EF compiles
    /// the query without opening a connection, so this runs anywhere, Docker or not.
    /// </summary>
    private static AppDbContext ConnectionlessContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=none;Username=none;Password=none")
            .Options);
}
