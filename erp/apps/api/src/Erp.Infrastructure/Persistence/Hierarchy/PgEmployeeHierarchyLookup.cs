using Erp.Core.Aggregates.Employees;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Erp.Infrastructure.Persistence.Hierarchy;

// TODO(scale): Single global advisory lock key for hierarchy mutations.
//   Acceptable today (low write volume, single-tenant). Revisit when either:
//     - multi-tenant lands → shard key by tenantId
//     - reparent QPS becomes hot → shard key by Owner subtree root id
//   Tracking: ARCHITECTURE_SPEC.md §15 (Hierarchy lock scaling).
public sealed class PgEmployeeHierarchyLookup : IEmployeeHierarchyLookup
{
    private const long HierarchyLockKey = 7_982_465_318_127_493_021L;

    private readonly AppDbContext _db;

    public PgEmployeeHierarchyLookup(AppDbContext db)
    {
        _db = db;
    }

    public Task AcquireHierarchyLockAsync(CancellationToken ct)
    {
        return _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})",
            new object[] { HierarchyLockKey },
            ct);
    }

    public async Task<IReadOnlyList<EmployeeId>> GetAncestorsAsync(EmployeeId employeeId, CancellationToken ct)
    {
        const string sql = """
            WITH RECURSIVE chain(id, parent_id, depth) AS (
                SELECT "Id", parent_id, 0
                FROM "Employees"
                WHERE "Id" = @id
                UNION ALL
                SELECT e."Id", e.parent_id, c.depth + 1
                FROM "Employees" e
                JOIN chain c ON e."Id" = c.parent_id
                WHERE c.depth < @maxDepth
            )
            SELECT id
            FROM chain
            WHERE id <> @id
            ORDER BY depth;
            """;

        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (_db.Database.CurrentTransaction is not null)
        {
            command.Transaction = _db.Database.CurrentTransaction.GetDbTransaction();
        }

        var idParam = new NpgsqlParameter("id", employeeId.Value);
        var depthParam = new NpgsqlParameter("maxDepth", EmployeeHierarchyPolicy.MaxAncestryWalk);
        command.Parameters.Add(idParam);
        command.Parameters.Add(depthParam);

        var ancestors = new List<EmployeeId>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ancestors.Add(new EmployeeId(reader.GetGuid(0)));
        }

        return ancestors;
    }
}
