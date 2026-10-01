namespace Erp.UseCases.Attendance.Holidays.ListHolidays;

public sealed record ListHolidaysQuery(DateOnly From, DateOnly To);
