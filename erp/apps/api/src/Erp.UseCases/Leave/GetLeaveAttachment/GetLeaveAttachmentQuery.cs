using Erp.UseCases.Common;

namespace Erp.UseCases.Leave.GetLeaveAttachment;

public sealed record GetLeaveAttachmentQuery(Guid LeaveRequestId, Caller Caller);

/// <summary>
/// The open stream and what to call it. The caller owns the stream and must dispose it — it is
/// a live handle on the stored file, not a buffer.
/// </summary>
public sealed record LeaveAttachmentContent(Stream Content, string FileName, string ContentType);
