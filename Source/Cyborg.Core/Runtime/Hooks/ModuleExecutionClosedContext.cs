using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Hooks;

internal sealed record ModuleExecutionClosedContext(
    ModuleInvocationContext Invocation,
    IModuleRuntime Runtime,
    bool Joined) : IModuleExecutionClosedContext
{
    public ModuleExecutionId ExecutionId => Invocation.ExecutionId;

    public ModuleExecutionId? ParentExecutionId => Invocation.ParentExecutionId;

    public string ModuleId => Invocation.ModuleId;

    public string? Name => Invocation.Name;

    public string? Group => Invocation.Group;

    public IModule Module => Invocation.Module;
}
