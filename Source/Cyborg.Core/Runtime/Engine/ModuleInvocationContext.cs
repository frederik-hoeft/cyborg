namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Stable identity carried by every runtime view that belongs to the same logical module invocation.
/// </summary>
internal sealed record ModuleInvocationContext(
    ModuleExecutionId ExecutionId,
    ModuleExecutionId? ParentExecutionId,
    string ModuleId,
    string? Name,
    string? Group,
    IModule Module);
