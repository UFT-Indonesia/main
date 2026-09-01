namespace Erp.Core.Interfaces;

/// <summary>
/// Where the bytes of a leave attachment live. Kept behind an interface so the domain and use
/// cases never learn whether that is a disk or a bucket — swapping the disk for object storage
/// later is one implementation, not a rewrite.
/// </summary>
public interface ILeaveAttachmentStorage
{
    /// <summary>
    /// Persists the stream and returns the opaque key that reads it back. The key is generated
    /// here, never derived from <paramref name="fileName"/> — a name that came from a browser
    /// has no business steering a filesystem path.
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct);

    /// <summary>Opens a previously saved attachment, or null when the key resolves to nothing.</summary>
    Task<Stream?> OpenAsync(string storageKey, CancellationToken ct);

    /// <summary>
    /// Best-effort cleanup of an orphan — a file saved for a request that then failed to be
    /// created. Never throws for a key that is already gone.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
