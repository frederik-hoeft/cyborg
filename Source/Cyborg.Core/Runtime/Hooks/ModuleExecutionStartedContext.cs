using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Hooks;

internal sealed record ModuleExecutionStartedContext(ModuleInvocationContext Invocation, IModuleRuntime Runtime)
    : IModuleExecutionStartedContext
{
    public ModuleExecutionId ExecutionId => Invocation.ExecutionId;

    public ModuleExecutionId? ParentExecutionId => Invocation.ParentExecutionId;

    public string ModuleId => Invocation.ModuleId;

    public string? Name => Invocation.Name;

    public string? Group => Invocation.Group;

    public IModule Module => Invocation.Module;
}
