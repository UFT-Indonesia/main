using Erp.Core.Aggregates.Attendance;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Attendance.Common;

namespace Erp.UseCases.Attendance.GetAttendancePolicy;

public static class GetAttendancePolicyHandler
{
    public static async Task<Result<AttendancePolicyResult>> Handle(
        GetAttendancePolicyQuery query,
        IReadRepository<AttendancePolicy> policies,
        CancellationToken ct)
    {
        var policy = await policies.GetByIdAsync(AttendancePolicyId.Singleton, ct);
        if (policy is null)
        {
            return new Result<AttendancePolicyResult>.NotFound("Attendance policy was not found.");
        }

        return new Result<AttendancePolicyResult>.Success(AttendancePolicyMapper.ToResult(policy));
    }
}
