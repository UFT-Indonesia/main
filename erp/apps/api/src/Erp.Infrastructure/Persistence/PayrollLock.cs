using Erp.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

/// <summary>Row locks on the single PayrollSettings row (id 1); released when the handler's transaction ends.</summary>
internal sealed class PayrollLock(AppDbContext db) : IPayrollLock
{
    public Task AcquireSharedAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("""SELECT 1 FROM "PayrollSettings" WHERE "Id" = 1 FOR SHARE""", ct);

    public Task AcquireExclusiveAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("""SELECT 1 FROM "PayrollSettings" WHERE "Id" = 1 FOR UPDATE""", ct);
}
