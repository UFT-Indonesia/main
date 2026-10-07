using Erp.Core.Aggregates.Attendance;
using Erp.Core.Aggregates.Attendance.Events;
using Erp.Core.Aggregates.Payroll;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Holidays.Common;
using Erp.UseCases.Payroll.Common;
using NodaTime;
using Wolverine;

namespace Erp.UseCases.Attendance.Holidays.RemoveHoliday;

public static class RemoveHolidayHandler
{
    public static async Task<Result<bool>> Handle(
        RemoveHolidayCommand command,
        IRepository<Holiday> holidays,
        IReadRepository<LeaveDeductionMonth> months,
        IMessageBus bus,
        IPayrollLock payrollLock,
        CancellationToken ct)
    {
        await payrollLock.AcquireSharedAsync(ct);

        var holiday = await holidays.FirstOrDefaultAsync(
            new HolidayOnDateSpec(LocalDate.FromDateOnly(command.Date)), ct);
        if (holiday is null)
        {
            return new Result<bool>.NotFound("No holiday on that date.");
        }

        if ((await LeaveDeductionEngine.ClosedMonthsAsync(months, ct)).Contains(LeaveDeductionMonth.MonthOf(holiday.Date)))
        {
            return new Result<bool>.Error("holiday.payroll_closed", "That date is in a month whose payroll is already closed.");
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
