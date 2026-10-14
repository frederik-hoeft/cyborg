using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Coordinates the transaction-bound invocation frame with environment scope selection.
/// Structural topology and view materialization are managed independently of invocation ancestry.
/// </summary>
internal sealed class RuntimeEnvironmentContext
{
    private readonly EnvironmentViewMaterializer _materializer;
    private readonly EnvironmentScopeSelector _selector;
    private readonly RuntimeInvocationFrame _frame;
    private readonly ActiveTransaction _active;

    public IRuntimeEnvironment GlobalEnvironment => _frame.GlobalEnvironment;

    public IRuntimeEnvironment ParentEnvironment => _frame.ParentEnvironment;

    public IRuntimeEnvironment Environment => _frame.Environment;

    private RuntimeEnvironmentContext(
        EnvironmentViewMaterializer materializer,
        EnvironmentScopeSelector selector,
        ActiveTransaction active,
        RuntimeInvocationFrame frame)
    {
        _materializer = materializer;
        _active = active;
        _frame = frame;
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
        return new RuntimeEnvironmentContext(materializer, selector, active, RuntimeInvocationFrame.CreateRoot(transactionalGlobal));
    }

    public RuntimeEnvironmentContext RebindTransaction(ActiveTransaction active)
    {
        ArgumentNullException.ThrowIfNull(active);
        RuntimeInvocationFrame reboundFrame = _frame.RebindTransaction(_materializer, active);
        return new RuntimeEnvironmentContext(_materializer, _selector, active, reboundFrame);
    }

    public RuntimeEnvironmentContext CreateChildFrame(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        IRuntimeEnvironment transactionalEnvironment = BindToCurrentTransaction(environment);
        return new RuntimeEnvironmentContext(_materializer, _selector, _active, _frame.CreateChild(transactionalEnvironment));
    }

    public IRuntimeEnvironment BindToCurrentTransaction(IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return _materializer.BindTransaction(environment, _active);
    }

    public IRuntimeEnvironment PrepareEnvironment(ModuleEnvironment moduleEnvironment, IReadOnlyCollection<string>? overrideResolutionTags = null) =>
        _selector.PrepareEnvironment(_frame, _active, moduleEnvironment, overrideResolutionTags);

    public IRuntimeEnvironment? ResolveEnvironmentReference(ModuleEnvironmentReference environmentReference) =>
        _selector.ResolveEnvironmentReference(_frame, _active, environmentReference);
}
