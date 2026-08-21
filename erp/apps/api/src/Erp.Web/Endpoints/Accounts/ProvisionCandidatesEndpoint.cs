using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Web.Endpoints.Accounts;

public sealed class ProvisionCandidateResponse
{
    public Guid EmployeeId { get; init; }
    public string FullName { get; init; } = default!;
    public string Role { get; init; } = default!;
}

public sealed class ListProvisionCandidatesResponse
{
    public IReadOnlyList<ProvisionCandidateResponse> Items { get; init; } = default!;
}

/// <summary>
/// Active employees without an account that the caller is allowed to provision.
/// Exists so Managers can pick an employee without access to the (salary-bearing,
/// Owner-only) employees list.
/// </summary>
[Authorize(Roles = "Owner,Manager")]
public sealed class ProvisionCandidatesEndpoint : EndpointWithoutRequest<ListProvisionCandidatesResponse>
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProvisionCandidatesEndpoint(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public override void Configure()
    {
        Get("/provision-candidates");
        Group<AccountsGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (CallerFactory.From(User) is not { } caller)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var linkedEmployeeIds = await _userManager.Users.AsNoTracking()
            .Where(u => u.EmployeeId != null)
            .Select(u => u.EmployeeId!.Value)
            .ToListAsync(ct);

        var employees = await _db.Employees.AsNoTracking()
            .Where(e => e.Status != EmployeeStatus.Terminated)
            .OrderBy(e => e.FullName)
            .ToListAsync(ct);

        var items = employees
            .Where(e => !linkedEmployeeIds.Contains(e.Id.Value) && AccountRules.CanManage(caller, e.Role, e.ParentId?.Value))
            .Select(e => new ProvisionCandidateResponse
            {
                EmployeeId = e.Id.Value,
                FullName = e.FullName,
                Role = e.Role.ToString(),
            })
            .ToList();

        await SendOkAsync(new ListProvisionCandidatesResponse { Items = items }, ct);
    }
}
