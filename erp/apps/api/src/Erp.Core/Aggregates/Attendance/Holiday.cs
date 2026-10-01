using Erp.Core.Aggregates.Attendance.Events;
using Erp.SharedKernel.Domain;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Identity;
using NodaTime;

namespace Erp.Core.Aggregates.Attendance;

/// <summary>
/// One date the whole office is closed. Only exceptions are stored — a normal Mon–Fri has no
/// row. The date is the natural key (unique index) and never changes: moving a holiday is
/// remove + declare, so every change to what is counted goes through
/// <see cref="HolidayCalendarChanged"/>.
/// </summary>
public sealed class Holiday : AggregateRoot<HolidayId>
{
    public const int NameMaxLength = 100;

    // EF Core constructor.
    private Holiday() { }

    private Holiday(HolidayId id, LocalDate date, string name, HolidayKind kind, Guid updatedByUserId, Instant updatedAtUtc)
        : base(id)
    {
        Date = date;
        Name = name;
        Kind = kind;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public LocalDate Date { get; private set; }

    /// <summary>What the day is, e.g. "HUT RI" or "Idul Fitri".</summary>
    public string Name { get; private set; } = default!;

    public HolidayKind Kind { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public Instant UpdatedAtUtc { get; private set; }

    public static Holiday Declare(LocalDate date, string name, HolidayKind kind, Guid updatedByUserId, Instant nowUtc)
    {
        var holiday = new Holiday(HolidayId.New(), date, EnsureValid(name, kind), kind, updatedByUserId, nowUtc);
        holiday.RaiseDomainEvent(new HolidayCalendarChanged(holiday.Id.Value, date));
        return holiday;
    }

    public void Rename(string name, HolidayKind kind, Guid updatedByUserId, Instant nowUtc)
    {
        Name = EnsureValid(name, kind);
        Kind = kind;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Call before deleting the row, so the date's leave gets recounted.</summary>
    public void Remove() => RaiseDomainEvent(new HolidayCalendarChanged(Id.Value, Date));

    private static string EnsureValid(string name, HolidayKind kind)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("holiday.name_required", "Holiday name is required.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException("holiday.name_length", $"Holiday name cannot exceed {NameMaxLength} characters.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("holiday.kind", "Holiday kind must be National or Collective.");
        }

        return trimmed;
    }
}
