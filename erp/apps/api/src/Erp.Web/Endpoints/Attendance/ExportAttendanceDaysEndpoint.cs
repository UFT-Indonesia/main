using System.Text;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.ExportAttendanceDays;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

[Authorize]
public sealed class ExportAttendanceDaysEndpoint : Endpoint<ExportAttendanceDaysRequest>
{
    private readonly IMessageBus _bus;

    public ExportAttendanceDaysEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Post("/days/export");
        Group<AttendanceGroup>();
    }

    public override async Task HandleAsync(ExportAttendanceDaysRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<ExportAttendanceDaysResult>>(
            new ExportAttendanceDaysQuery(
                req.Items.Select(i => new AttendanceDayKey(i.EmployeeId, i.Date)).ToList()),
            ct);

        if (result is Result<ExportAttendanceDaysResult>.Success s)
        {
            var csv = BuildCsv(s.Value.Rows);
            HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=\"attendance-days.csv\"";
            await SendStringAsync(csv, contentType: "text/csv; charset=utf-8", cancellation: ct);
            return;
        }

        if (result is Result<ExportAttendanceDaysResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }

    private static string BuildCsv(IReadOnlyList<ExportAttendanceDayRowResult> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Employee,Date,TapIn,TapOut,Status");

        foreach (var row in rows)
        {
            builder
                .Append(EscapeCsv(row.EmployeeFullName)).Append(',')
                .Append(EscapeCsv(row.Date)).Append(',')
                .Append(EscapeCsv(row.TapIn)).Append(',')
                .Append(EscapeCsv(row.TapOut)).Append(',')
                .Append(EscapeCsv(row.Status))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
