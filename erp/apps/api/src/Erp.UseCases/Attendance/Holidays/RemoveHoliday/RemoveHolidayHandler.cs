using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.Holidays.RemoveHoliday;

public static class RemoveHolidayHandler
{
    public static async Task<Result<bool>> Handle(
        RemoveHolidayCommand command,
        IRepository<Holiday> holidays,
        IMessageBus bus,
        CancellationToken ct)
    {
        var holiday = await holidays.FirstOrDefaultAsync(
            new HolidayOnDateSpec(LocalDate.FromDateOnly(command.Date)), ct);
        if (holiday is null)
        {
            return new Result<bool>.NotFound("No holiday on that date.");
        }

        holiday.Remove();
        await holidays.DeleteAsync(holiday, ct);

        foreach (var domainEvent in holiday.DomainEvents.OfType<HolidayCalendarChanged>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<bool>.Success(true);
    }
}
