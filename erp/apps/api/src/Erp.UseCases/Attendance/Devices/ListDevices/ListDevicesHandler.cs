using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.UseCases.Attendance.Devices.Common;

namespace Erp.UseCases.Attendance.Devices.ListDevices;

public static class ListDevicesHandler
{
    public static async Task<Result<ListDevicesResult>> Handle(
        ListDevicesQuery query,
        IReadRepository<AttendanceDevice> devices,
        CancellationToken ct)
    {
        var items = await devices.ListAsync(new AllAttendanceDevicesSpec(), ct);

        return new Result<ListDevicesResult>.Success(new ListDevicesResult
        {
            Items = items.Select(AttendanceDeviceResult.From).ToList(),
        });
    }
}

public sealed class ListDevicesResult
{
    public IReadOnlyList<AttendanceDeviceResult> Items { get; init; } = [];
}
