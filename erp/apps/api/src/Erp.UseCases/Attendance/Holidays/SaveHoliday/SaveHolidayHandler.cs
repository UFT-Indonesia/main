using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.Holidays.SaveHoliday;

public static class SaveHolidayHandler
{
    public static async Task<Result<HolidayResult>> Handle(
        SaveHolidayCommand command,
        IRepository<Holiday> holidays,
        IClock clock,
        IMessageBus bus,
        CancellationToken ct)
    {
        if (!Enum.TryParse<HolidayKind>(command.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return new Result<HolidayResult>.Error("holiday.kind", "Holiday kind must be National or Collective.");
        }

        var date = LocalDate.FromDateOnly(command.Date);
        var now = clock.GetCurrentInstant();
        var holiday = await holidays.FirstOrDefaultAsync(new HolidayOnDateSpec(date), ct);

        try
        {
            if (holiday is null)
            {
                holiday = Holiday.Declare(date, command.Name, kind, command.ChangedByUserId, now);
                await holidays.AddAsync(holiday, ct);
            }
            else
            {
                holiday.Rename(command.Name, kind, command.ChangedByUserId, now);
                await holidays.UpdateAsync(holiday, ct);
            }
        }
        catch (DomainException ex)
        {
            return new Result<HolidayResult>.Error(ex.Code ?? "holiday.validation", ex.Message);
        }

        foreach (var domainEvent in holiday.DomainEvents.OfType<HolidayCalendarChanged>())
        {
            await bus.PublishAsync(domainEvent);
        }

        return new Result<HolidayResult>.Success(HolidayResult.From(holiday));
    }
}
