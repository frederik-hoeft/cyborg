using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Hooks;

/// <summary>Describes an invocation with a definite result before structured reconciliation closes it.</summary>
public interface IModuleExecutionCompletedContext : IModuleExecutionLifecycleContext
{
    IModuleExecutionResult Result { get; }
}
