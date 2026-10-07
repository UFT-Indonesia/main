using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using Erp.UseCases.Payroll.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.Holidays.SaveHoliday;

public static class SaveHolidayHandler
{
    public static async Task<Result<HolidayResult>> Handle(
        SaveHolidayCommand command,
        IRepository<Holiday> holidays,
        IReadRepository<LeaveDeductionMonth> months,
        IClock clock,
        IMessageBus bus,
        IPayrollLock payrollLock,
        CancellationToken ct)
    {
        await payrollLock.AcquireSharedAsync(ct);

        if (!Enum.TryParse<HolidayKind>(command.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return new Result<HolidayResult>.Error("holiday.kind", "Holiday kind must be National or Collective.");
        }

        var date = LocalDate.FromDateOnly(command.Date);
        var now = clock.GetCurrentInstant();
        var holiday = await holidays.FirstOrDefaultAsync(new HolidayOnDateSpec(date), ct);

        // Declaring a day changes what leave counts and costs, so it can't reach into a month the Owner
        // has closed (GSS03 decision 14). Renaming one already there changes no count.
        if (holiday is null
            && (await LeaveDeductionEngine.ClosedMonthsAsync(months, ct)).Contains(LeaveDeductionMonth.MonthOf(date)))
        {
            return new Result<HolidayResult>.Error(
                "holiday.payroll_closed", "That date is in a month whose payroll is already closed.");
        }

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
