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
