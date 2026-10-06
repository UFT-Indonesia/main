using Erp.Core.Aggregates.Leave;
using Erp.Core.Interfaces;
using Erp.SharedKernel.Domain.Errors;

namespace Erp.Web.Endpoints.Overtime;

/// <summary>
/// Streams an uploaded proof to the same store leave uses, under the same rules: 10 MB, PDF/JPEG/PNG,
/// checked by the file's own leading bytes rather than the client's Content-Type.
/// </summary>
internal static class AttachmentUpload
{
    public static async Task<LeaveAttachment?> SaveAsync(IFormFile? upload, ILeaveAttachmentStorage storage, CancellationToken ct)
    {
        if (upload is not { Length: > 0 })
        {
            return null;
        }

        if (upload.Length > LeaveRequest.AttachmentMaxBytes
            || !LeaveRequest.AllowedAttachmentContentTypes.Contains(upload.ContentType))
        {
            throw new DomainException(
                "overtime.attachment_invalid",
                $"The proof must be a PDF, JPEG or PNG of at most {LeaveRequest.AttachmentMaxBytes / (1024 * 1024)}MB.");
        }

        await using var stream = upload.OpenReadStream();
        var header = new byte[LeaveAttachment.SignatureBytesToRead];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        if (!LeaveAttachment.MatchesSignature(upload.ContentType, header.AsSpan(0, read)))
        {
            throw new DomainException("overtime.attachment_invalid", "The proof must be a PDF, JPEG or PNG.");
        }

        stream.Position = 0;
        return LeaveAttachment.Create(
            await storage.SaveAsync(stream, upload.FileName, ct), upload.FileName, upload.ContentType, upload.Length);
    }
}
