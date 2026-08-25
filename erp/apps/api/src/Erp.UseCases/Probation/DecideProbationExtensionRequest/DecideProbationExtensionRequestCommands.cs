using Erp.UseCases.Common;

namespace Erp.UseCases.Probation.DecideProbationExtensionRequest;

public sealed record ApproveProbationExtensionCommand(Guid RequestId, Caller Caller, string? Note);

public sealed record DenyProbationExtensionCommand(Guid RequestId, Caller Caller, string? Note);

public sealed record CancelProbationExtensionCommand(Guid RequestId, Caller Caller, string? Note);
