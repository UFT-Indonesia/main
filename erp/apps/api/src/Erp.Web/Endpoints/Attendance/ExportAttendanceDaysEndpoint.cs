using System.Text;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Common;
using Erp.UseCases.Attendance.ExportAttendanceDays;
using Erp.UseCases.Common.Filtering;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Attendance;

/// <summary>Exports the period as shown. Staff are scoped to their own rows by the calendar's employee spec.</summary>
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
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<ExportAttendanceDaysResult>>(
            new ExportAttendanceDaysQuery(
                req.From,
                req.To,
                FilterBinding.ParseOrThrow(req.Filter),
                caller,
                req.ProblemsOnly),
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
            if (e.Code == ResultErrors.Forbidden || e.Code == FilterErrors.FieldForbidden)
            {
                await SendForbiddenAsync(ct);
                return;
            }

            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }

    internal static string BuildCsv(IReadOnlyList<ExportAttendanceDayRowResult> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Employee,Date,Punches,Status,LeaveType");

        foreach (var row in rows)
        {
            builder
                .Append(EscapeCsv(row.EmployeeFullName)).Append(',')
                .Append(EscapeCsv(row.Date)).Append(',')
                .Append(EscapeCsv(row.Punches)).Append(',')
                .Append(EscapeCsv(row.Status)).Append(',')
                .Append(EscapeCsv(row.LeaveType))
                .AppendLine();
        }

        return builder.ToString();
    }

    // Values starting with =, +, -, or @ are interpreted as formulas by Excel/Sheets.
    // Prefix a leading single quote to neutralize that — OWASP CSV injection mitigation.
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@'];

    internal static string EscapeCsv(string value)
    {
        if (value.Length > 0 && FormulaTriggers.Contains(value[0]))
        {
            value = "'" + value;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
