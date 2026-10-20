using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Hooks;

/// <summary>Stable identity and runtime state shared by structured execution lifecycle events.</summary>
public interface IModuleExecutionLifecycleContext
{
    ModuleExecutionId ExecutionId { get; }

    ModuleExecutionId? ParentExecutionId { get; }

    string ModuleId { get; }

    /// <summary>Module name captured when the invocation became active.</summary>
    string? Name { get; }

    /// <summary>Module group captured when the invocation became active.</summary>
    string? Group { get; }

    IModule Module { get; }

    /// <summary>Immutable definition-site origin of the invoked module, when known.</summary>
    ModuleOrigin? Origin => null;

    IModuleRuntime Runtime { get; }
}
