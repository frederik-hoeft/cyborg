using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Represents the runtime invocation-frame hierarchy. Environment selection, logical graph access,
/// and structural view materialization are delegated to their respective owners.
/// </summary>
internal sealed class RuntimeEnvironmentContext
{
    private readonly EnvironmentViewMaterializer _materializer;
    private readonly EnvironmentScopeSelector _selector;
    private readonly RuntimeEnvironmentContext? _parent;
    private readonly ActiveTransaction _active;

    public IRuntimeEnvironment GlobalEnvironment { get; }

    public IRuntimeEnvironment ParentEnvironment => _parent?.Environment ?? GlobalEnvironment;

    public IRuntimeEnvironment Environment { get; }

    public VariableSyntaxBuilder SyntaxFactory => Environment.SyntaxFactory;

    internal RuntimeEnvironmentContext? ParentFrame => _parent;

    private RuntimeEnvironmentContext(
        EnvironmentViewMaterializer materializer,
        EnvironmentScopeSelector selector,
        ActiveTransaction active,
        RuntimeEnvironmentContext? parent,
        IRuntimeEnvironment globalEnvironment,
        IRuntimeEnvironment environment)
    {
        _materializer = materializer;
        _active = active;
        _parent = parent;
        GlobalEnvironment = globalEnvironment;
        Environment = environment;
        _selector = selector;
    }

    public static RuntimeEnvironmentContext CreateRoot(
        GlobalRuntimeEnvironment globalEnvironment,
        IRuntimeEnvironmentFactory environmentFactory,
        RuntimeEnvironmentTransactionParticipant environments,
        ActiveTransaction active,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(globalEnvironment);
        ArgumentNullException.ThrowIfNull(environmentFactory);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ILogger logger = loggerFactory.CreateLogger("cyborg.core.runtime");
        RuntimeEnvironmentGraphAccess graph = new(environments);
        graph.ValidateGlobalEnvironment(globalEnvironment, active);
        EnvironmentViewMaterializer materializer = new(environmentFactory, graph);
        IRuntimeEnvironment transactionalGlobal = materializer.BindTransaction(globalEnvironment, active);
        EnvironmentScopeSelector selector = new(graph, materializer, logger);
        return new RuntimeEnvironmentContext(materializer, selector, active, parent: null, transactionalGlobal, transactionalGlobal);
    }

    public RuntimeEnvironmentContext CreateTransactionView(ActiveTransaction active)
    {
        ArgumentNullException.ThrowIfNull(active);
        RuntimeEnvironmentContext? parent = _parent?.CreateTransactionView(active);
        IRuntimeEnvironment globalEnvironment = _materializer.BindTransaction(GlobalEnvironment, active);
        IRuntimeEnvironment environment = _materializer.BindTransaction(Environment, active);
        return new RuntimeEnvironmentContext(_materializer, _selector, active, parent, globalEnvironment, environment);
    }

    public RuntimeEnvironmentContext CreateChild(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment transactionalEnvironment = BindEnvironment(environment);
        return new RuntimeEnvironmentContext(_materializer, _selector, _active, this, GlobalEnvironment, transactionalEnvironment);
    }

    public IRuntimeEnvironment BindEnvironment(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _materializer.BindTransaction(environment, _active);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        _selector.PrepareEnvironment(this, _active, moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        _selector.ResolveEnvironmentReference(this, _active, environmentReference);
}
