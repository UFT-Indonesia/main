using Erp.Core.Aggregates.Employees;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence;
using Erp.SharedKernel.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Web.Endpoints.Accounts;

[Authorize(Roles = "Owner,Manager")]
public sealed class CreateAccountEndpoint : Endpoint<CreateAccountRequest, CreateAccountResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public CreateAccountEndpoint(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public override void Configure()
    {
        Post("/");
        Group<AccountsGroup>();
    }

    public override async Task HandleAsync(CreateAccountRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
        {
            ThrowError("Username is required.", 400);
        }

        var employee = await _db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == new EmployeeId(req.EmployeeId), ct);
        if (employee is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (employee.Status == EmployeeStatus.Terminated)
        {
            ThrowError("Cannot create an account for a terminated employee.", 400);
        }

        if (!AccountRules.CanManage(User, employee.Role))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        if (await _userManager.Users.AnyAsync(u => u.EmployeeId == req.EmployeeId, ct))
        {
            ThrowError("This employee already has an account.", 400);
        }

        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email is not null && await _userManager.FindByEmailAsync(email) is not null)
        {
            ThrowError("An account with this email already exists.", 400);
        }

        var user = new ApplicationUser
        {
            UserName = req.Username.Trim(),
            Email = email,
            EmailConfirmed = email is not null,
            FullName = employee.FullName,
            EmployeeId = req.EmployeeId,
            MustChangePassword = true,
        };

        var tempPassword = TempPassword.Generate();
        var createResult = await _userManager.CreateAsync(user, tempPassword);
        if (!createResult.Succeeded)
        {
            ThrowError(string.Join(" ", createResult.Errors.Select(e => e.Description)), 400);
        }

        await _userManager.AddToRoleAsync(user, employee.Role.ToString());

        await SendAsync(new CreateAccountResponse
        {
            Account = new AccountResponse
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email,
                FullName = user.FullName,
                EmployeeId = user.EmployeeId,
                Role = employee.Role.ToString(),
                IsEnabled = true,
                MustChangePassword = true,
            },
            TempPassword = tempPassword,
        }, 201, ct);
    }
}
