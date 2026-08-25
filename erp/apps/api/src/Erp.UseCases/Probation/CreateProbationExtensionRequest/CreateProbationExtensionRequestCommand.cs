using Erp.UseCases.Common;

namespace Erp.UseCases.Probation.CreateProbationExtensionRequest;

public sealed record CreateProbationExtensionRequestCommand(
    Guid EmployeeId,
    DateOnly ProposedEndsOn,
    string Reason,
    Caller Caller);
