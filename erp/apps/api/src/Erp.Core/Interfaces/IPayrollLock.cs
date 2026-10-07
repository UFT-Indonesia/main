namespace Erp.Core.Interfaces;

/// <summary>
/// Orders month-closing against everything that must not slip into a month while it closes: leave
/// decisions and edits, and holiday changes take the shared lock, closing takes the exclusive one. Both
/// are held to the end of the handler's transaction, so whoever waits re-reads the closed months fresh.
/// </summary>
public interface IPayrollLock
{
    Task AcquireSharedAsync(CancellationToken ct);

    Task AcquireExclusiveAsync(CancellationToken ct);
}
