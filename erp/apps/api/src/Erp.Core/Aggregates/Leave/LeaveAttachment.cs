using Erp.SharedKernel.Domain.Errors;

namespace Erp.Core.Aggregates.Leave;

/// <summary>
/// The supporting document on a Sick leave request — a doctor's note, scanned or photographed.
/// Only where the bytes live and what they were called; writing and reading them is the
/// infrastructure's job (ILeaveAttachmentStorage), because the domain has no business knowing
/// whether that is a disk, a bucket, or a test double.
/// </summary>
/// <param name="StorageKey">
/// Opaque handle the storage layer resolves back to the bytes. Never shown to a user and never
/// built from <paramref name="FileName"/> — a filename comes from outside and would drag path
/// traversal into whatever ends up resolving it.
/// </param>
/// <param name="FileName">The name the uploader's file had, kept only to name the download.</param>
/// <param name="ContentType">Validated against <see cref="LeaveRequest.AllowedAttachmentContentTypes"/>.</param>
/// <param name="SizeBytes">Size as stored, for display.</param>
public sealed record LeaveAttachment(
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes)
{
    /// <summary>Longest filename kept. Anything longer is truncated rather than rejected.</summary>
    public const int FileNameMaxLength = 200;

    /// <summary>
    /// Magic-number signatures for each allowed content type. A client's declared Content-Type
    /// header is just a string it typed in — checking it alone lets anyone store, say, an HTML
    /// file under "application/pdf". The actual bytes don't lie the same way.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte[][]> SignaturesByContentType =
        new Dictionary<string, byte[][]>
        {
            ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46]], // "%PDF"
            ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
            ["image/png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        };

    /// <summary>Longest signature above, so callers know how many leading bytes to read.</summary>
    public const int SignatureBytesToRead = 8;

    /// <summary>True when the file's own leading bytes match what <paramref name="contentType"/> claims.</summary>
    public static bool MatchesSignature(string contentType, ReadOnlySpan<byte> header)
    {
        if (!SignaturesByContentType.TryGetValue(contentType, out var signatures))
        {
            return false;
        }

        foreach (var signature in signatures)
        {
            if (header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature))
            {
                return true;
            }
        }

        return false;
    }

    public static LeaveAttachment Create(
        string storageKey, string fileName, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainException("leave.attachment_key", "Attachment storage key is required.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("leave.attachment_empty", "The uploaded file is empty.");
        }

        if (sizeBytes > LeaveRequest.AttachmentMaxBytes)
        {
            throw new DomainException(
                "leave.attachment_too_large",
                $"The file exceeds the {LeaveRequest.AttachmentMaxBytes / (1024 * 1024)}MB limit.");
        }

        if (!LeaveRequest.AllowedAttachmentContentTypes.Contains(contentType))
        {
            throw new DomainException(
                "leave.attachment_type",
                "The file must be a PDF, JPEG, or PNG.");
        }

        // Only the leaf, and only the part of it that fits: the browser sends whatever the user's
        // filesystem had, which on some clients is a full path.
        var safeName = Path.GetFileName(fileName?.Trim() ?? string.Empty);
        if (string.IsNullOrEmpty(safeName))
        {
            safeName = "attachment";
        }

        if (safeName.Length > FileNameMaxLength)
        {
            safeName = safeName[^FileNameMaxLength..];
        }

        return new LeaveAttachment(storageKey, safeName, contentType, sizeBytes);
    }
}
