using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Domain.Errors;
using Erp.SharedKernel.Domain.Results;
using Erp.SharedKernel.Identity;
using Erp.UseCases.Employees.Common;
using Erp.UseCases.Employees.UpdateEmployee;
using Erp.Web.Endpoints.Accounts;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Erp.Web.Endpoints.Employees;

/// <summary>
/// Scoped by <see cref="AccountRules.CanManage"/>: Owner updates anyone, Manager only
/// Staff. The requested role and any new parent are checked too, so a Manager cannot
/// promote a Staff member out of their own reach or restructure the org chart.
/// Salary is Owner-only — a Manager sending any wage field is rejected outright.
/// </summary>
[Authorize(Roles = "Owner,Manager")]
public sealed class UpdateEmployeeEndpoint : Endpoint<UpdateEmployeeRouteRequest, EmployeeResponse>
{
    private readonly IMessageBus _bus;
    private readonly AppDbContext _db;

    public UpdateEmployeeEndpoint(IMessageBus bus, AppDbContext db)
    {
        _bus = bus;
        _db = db;
    }

    public override void Configure()
    {
        Put("/{Id:guid}");
        Group<EmployeeGroup>();
    }

    public override async Task HandleAsync(UpdateEmployeeRouteRequest req, CancellationToken ct)
    {
        if (!await IsAllowedAsync(req, ct))
        {
            return;
        }

        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _bus.InvokeAsync<Result<EmployeeResult>>(new UpdateEmployeeCommand(
            req.Id,
            req.FullName,
            req.Npwp,
            req.MonthlyWageAmount,
            req.EffectiveSalaryFrom,
            req.Role,
            req.ParentId,
            caller), ct);

        if (result is Result<EmployeeResult>.Success s)
        {
            await SendOkAsync(EmployeeResponseMapper.ToResponse(s.Value, User), ct);
            return;
        }

        if (result is Result<EmployeeResult>.NotFound)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (result is Result<EmployeeResult>.Error e)
        {
            throw new DomainException(e.Code, e.Message);
        }

        throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}");
    }

    /// <summary>Sends the failing response itself; returns false when the caller is not allowed through.</summary>
    private async Task<bool> IsAllowedAsync(UpdateEmployeeRouteRequest req, CancellationToken ct)
    {
        // Only Owner may set pay. Managers never receive it on read, so they never send it back.
        if (!User.IsInRole(nameof(EmployeeRole.Owner))
            && (req.MonthlyWageAmount.HasValue || req.EffectiveSalaryFrom.HasValue))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        var targetId = new EmployeeId(req.Id);
        var target = await _db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == targetId, ct);
        if (target is null)
        {
            await SendNotFoundAsync(ct);
            return false;
        }

        if (!AccountRules.CanManage(User, target.Role))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        // Guard the *requested* role as well, otherwise a Manager could promote a Staff
        // member to Owner and escape their own scope in a single call.
        if (Enum.TryParse<EmployeeRole>(req.Role, ignoreCase: true, out var requestedRole)
            && !AccountRules.CanManage(User, requestedRole))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        return await IsParentChangeAllowedAsync(req, target, ct);
    }

    private async Task<bool> IsParentChangeAllowedAsync(
        UpdateEmployeeRouteRequest req,
        Employee target,
        CancellationToken ct)
    {
        if (!req.ParentId.HasValue)
        {
            return true;
        }

        var newParentId = new EmployeeId(req.ParentId.Value);
        if (target.ParentId == newParentId)
        {
            return true;
        }

        var parentRole = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == newParentId)
            .Select(e => (EmployeeRole?)e.Role)
            .FirstOrDefaultAsync(ct);

        // Missing parent is the handler's error to report, with its own error code.
        if (parentRole is null)
        {
            return true;
        }

        // In practice this makes reparenting Owner-only: a Manager only passes CanManage for a
        // Staff parent, and the max-depth rule already forbids Staff-under-Staff.
        if (!AccountRules.CanManage(User, parentRole.Value))
        {
            await SendForbiddenAsync(ct);
            return false;
        }

        return true;
    }
}
