using Erp.UseCases.Attendance.ExportAttendanceDays;
using Erp.Web.Endpoints.Attendance;
using FluentAssertions;

namespace Erp.UnitTests.Endpoints;

public class ExportAttendanceDaysEndpointTests
{
    [Theory]
    [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-1+1", "'-1+1")]
    [InlineData("@SUM(1,1)", "\"'@SUM(1,1)\"")]
    [InlineData("Alice", "Alice")]
    public void EscapeCsv_neutralizes_formula_injection_prefixes(string input, string expected)
    {
        ExportAttendanceDaysEndpoint.EscapeCsv(input).Should().Be(expected);
    }

    [Fact]
    public void BuildCsv_neutralizes_formula_injection_in_employee_name()
    {
        var rows = new List<ExportAttendanceDayRowResult>
        {
            new()
            {
                EmployeeFullName = "=cmd|'/c calc'!A1",
                Date = "2026-01-01",
                TapIn = "08:00",
                TapOut = "17:00",
                Status = "Complete",
            },
        };

        var csv = ExportAttendanceDaysEndpoint.BuildCsv(rows);

        csv.Should().Contain("'=cmd|'/c calc'!A1");
        csv.Should().NotContain("\n=cmd");
    }
}
