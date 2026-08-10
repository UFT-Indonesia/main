using System.Text;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Employees.ExportEmployeeAuditLog;
using Erp.Web.Endpoints.Attendance;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

[Authorize(Roles = "Owner")]
public sealed class ExportEmployeeAuditLogEndpoint : Endpoint<ExportEmployeeAuditLogRequest>
{
    private readonly IMessageBus _bus;

    public ExportEmployeeAuditLogEndpoint(IMessageBus bus)
    {
        _bus = bus;
    }

    public override void Configure()
    {
        Get("/audit-log/export");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(ExportEmployeeAuditLogRequest req, CancellationToken ct)
    {
        var result = await _bus.InvokeAsync<Result<ExportEmployeeAuditLogResult>>(new ExportEmployeeAuditLogQuery(
            req.EmployeeId,
            req.DateFrom,
            req.DateTo,
            req.EventType), ct);

        if (result is Result<ExportEmployeeAuditLogResult>.Success s)
        {
            var csv = BuildCsv(s.Value.Rows);
            HttpContext.Response.Headers["Content-Disposition"] = "attachment; filename=\"employee-audit-log.csv\"";
            await SendStringAsync(csv, contentType: "text/csv; charset=utf-8", cancellation: ct);
            return;
        }

        if (result is Result<ExportEmployeeAuditLogResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }

    // Reuses ExportAttendanceDaysEndpoint's CSV-injection-safe escaping rather than
    // re-implementing it.
    internal static string BuildCsv(IReadOnlyList<ExportEmployeeAuditLogRowResult> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Employee,EventType,OccurredAtUtc,ChangedBy,OldValue,NewValue");

        foreach (var row in rows)
        {
            builder
                .Append(ExportAttendanceDaysEndpoint.EscapeCsv(row.EmployeeFullName)).Append(',')
                .Append(ExportAttendanceDaysEndpoint.EscapeCsv(row.EventType)).Append(',')
                .Append(ExportAttendanceDaysEndpoint.EscapeCsv(row.OccurredAtUtc)).Append(',')
                .Append(ExportAttendanceDaysEndpoint.EscapeCsv(row.ActorName)).Append(',')
                .Append(ExportAttendanceDaysEndpoint.EscapeCsv(row.OldValueJson)).Append(',')
                .Append(ExportAttendanceDaysEndpoint.EscapeCsv(row.NewValueJson))
                .AppendLine();
        }

        return builder.ToString();
    }
}
