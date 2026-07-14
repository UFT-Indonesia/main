namespace Erp.UseCases.Attendance.ListAttendanceDays;

public sealed record ListAttendanceDaysQuery(
    int Page,
    int PageSize,
    string? EmployeeSearch,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    string? Status);
