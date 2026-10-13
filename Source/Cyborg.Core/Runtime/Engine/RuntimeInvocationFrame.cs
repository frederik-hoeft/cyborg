using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// An immutable frame in the module execution hierarchy. Its parent is the enclosing invocation's
/// environment view, not the structural parent of the logical environment.
/// </summary>
internal sealed class RuntimeInvocationFrame
{
    public RuntimeInvocationFrame? Parent { get; }

    public IRuntimeEnvironment Environment { get; }

    public IRuntimeEnvironment GlobalEnvironment { get; }

    public IRuntimeEnvironment ParentEnvironment => Parent?.Environment ?? GlobalEnvironment;

    private RuntimeInvocationFrame(RuntimeInvocationFrame? parent, IRuntimeEnvironment globalEnvironment, IRuntimeEnvironment environment)
    {
        Parent = parent;
        GlobalEnvironment = globalEnvironment;
        Environment = environment;
    }

    public static RuntimeInvocationFrame CreateRoot(IRuntimeEnvironment globalEnvironment) => new(parent: null, globalEnvironment, globalEnvironment);

    public RuntimeInvocationFrame CreateChild(IRuntimeEnvironment environment) => new(this, GlobalEnvironment, environment);

    /// <summary>
    /// Rebinds every invocation view to another transaction while retaining its namespace, override
    /// tags, and invocation ancestry. Structural parents are rebound by the environment factory.
    /// </summary>
    public RuntimeInvocationFrame RebindTransaction(EnvironmentViewMaterializer materializer, ActiveTransaction active)
    {
        RuntimeInvocationFrame? parent = Parent?.RebindTransaction(materializer, active);
        IRuntimeEnvironment globalEnvironment = materializer.BindTransaction(GlobalEnvironment, active);
        IRuntimeEnvironment environment = materializer.BindTransaction(Environment, active);
        return new RuntimeInvocationFrame(parent, globalEnvironment, environment);
    }
}
