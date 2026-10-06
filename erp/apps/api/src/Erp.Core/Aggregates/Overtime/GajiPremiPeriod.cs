using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Overtime;

/// <summary>
/// A Gaji Premi period the Owner has closed. Only closed periods are stored — an open one is
/// just the absence of a row. A period is two calendar months starting in an odd month
/// (Jan–Feb, Mar–Apr, … Nov–Dec), paid on the 15th of the month after it ends. Closing is
/// irreversible.
/// </summary>
public sealed class GajiPremiPeriod : AggregateRoot<GajiPremiPeriodId>
{
    // EF Core constructor.
    private GajiPremiPeriod() { }

    private GajiPremiPeriod(GajiPremiPeriodId id, LocalDate startDate, Guid closedByUserId, string closedByName, Instant closedAtUtc)
        : base(id)
    {
        StartDate = startDate;
        ClosedByUserId = closedByUserId;
        ClosedByName = closedByName;
        ClosedAtUtc = closedAtUtc;
    }

    public LocalDate StartDate { get; private set; }

    public Guid ClosedByUserId { get; private set; }

    public string ClosedByName { get; private set; } = default!;

    public Instant ClosedAtUtc { get; private set; }

    public static GajiPremiPeriod Close(LocalDate startDate, Guid closedByUserId, string closedByName, Instant nowUtc) =>
        new(GajiPremiPeriodId.New(), startDate, closedByUserId, closedByName, nowUtc);

    /// <summary>First day of the period holding <paramref name="date"/>.</summary>
    public static LocalDate StartOf(LocalDate date) =>
        new(date.Year, date.Month % 2 == 1 ? date.Month : date.Month - 1, 1);

    public static LocalDate EndOf(LocalDate start) => start.PlusMonths(2).PlusDays(-1);

    public static LocalDate PayoutDate(LocalDate start) => start.PlusMonths(2).With(DateAdjusters.DayOfMonth(15));
}
