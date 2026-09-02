namespace Cyborg.Core.Runtime.Hooks;

/// <summary>Describes an invocation after its owning fork has joined or discarded it.</summary>
public interface IModuleExecutionClosedContext : IModuleExecutionLifecycleContext
{
    /// <summary>
    /// <see langword="true"/> when the invocation reconciled into its owner; otherwise the invocation was discarded.
    /// </summary>
    bool Joined { get; }
}
