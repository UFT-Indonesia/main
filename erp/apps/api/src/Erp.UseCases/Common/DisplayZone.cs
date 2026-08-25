using NodaTime;

namespace Erp.UseCases.Common;

/// <summary>
/// The single timezone the org operates and reads dates in. "Today" for a business rule has to
/// mean today *here* — a UTC date rolls over at 07:00 local, which would confirm a probation or
/// roll a leave year over on the wrong day for everyone working a morning. Single-site org, so
/// this is a constant rather than a per-employee lookup.
/// </summary>
public static class DisplayZone
{
    public static readonly DateTimeZone Jakarta = DateTimeZoneProviders.Tzdb["Asia/Jakarta"];

    public static LocalDate Today(IClock clock) => clock.GetCurrentInstant().InZone(Jakarta).Date;
}
