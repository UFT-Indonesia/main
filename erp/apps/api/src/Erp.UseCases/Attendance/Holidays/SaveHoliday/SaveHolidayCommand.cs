namespace Erp.UseCases.Attendance.Holidays.SaveHoliday;

/// <summary>Declares a holiday on <see cref="Date"/>, or renames the one already there.</summary>
public sealed record SaveHolidayCommand(DateOnly Date, string Name, string Kind, Guid ChangedByUserId);
