using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Modules.Runtime;

public sealed class RootModuleRuntime : ModuleRuntimeBase
{
    private protected override IModuleRuntime Root => this;

    private protected override IModuleRuntime? Parent => null;

    public RootModuleRuntime(
        GlobalRuntimeEnvironment defaultEnvironment,
        ILoggerFactory loggerFactory,
        IServiceProvider? serviceProvider = null)
        : this(CreateStandaloneComposition(defaultEnvironment, loggerFactory), serviceProvider)
    {
    }

    internal RootModuleRuntime(
        GlobalRuntimeEnvironment defaultEnvironment,
        IRuntimeEnvironmentFactory environmentFactory,
        ModuleRuntimeOperations operations,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
        : this(CreateState(defaultEnvironment, environmentFactory, operations, loggerFactory), operations, serviceProvider)
    {
    }

    private RootModuleRuntime(RootRuntimeComposition composition, IServiceProvider? serviceProvider)
        : this(composition.State, composition.Operations, serviceProvider)
    {
    }

    private RootModuleRuntime(
        RootRuntimeState state,
        ModuleRuntimeOperations operations,
        IServiceProvider? serviceProvider)
        : base(state.EnvironmentContext, operations, state.Transaction, serviceProvider)
    {
    }

    private static RootRuntimeComposition CreateStandaloneComposition(
        GlobalRuntimeEnvironment defaultEnvironment,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        IRuntimeEnvironmentFactory environmentFactory = new DefaultRuntimeEnvironmentFactory(
            defaultEnvironment.SyntaxFactory,
            taggedStringConversionObserver: null);
        IRuntimeModuleRegistry moduleRegistry = new RuntimeModuleRegistry();
        ModuleRuntimeOperations operations = new(
            new ModuleArtifactPublisher(loggerFactory),
            new ModuleContextExecutor(defaultEnvironment.SyntaxFactory, environmentFactory, loggerFactory),
            new ModuleExecutionDispatcher(environmentFactory, loggerFactory),
            moduleRegistry);
        return new RootRuntimeComposition(
            CreateState(defaultEnvironment, environmentFactory, operations, loggerFactory),
            operations);
    }

    private static RootRuntimeState CreateState(
        GlobalRuntimeEnvironment defaultEnvironment,
        IRuntimeEnvironmentFactory environmentFactory,
        ModuleRuntimeOperations operations,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);
        ArgumentNullException.ThrowIfNull(environmentFactory);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        RuntimeEnvironmentTransactionParticipant environments = new();
        TransactionCoordinator coordinator = new([environments, operations.ModuleRegistry.Participant]);
        RuntimeEnvironmentNode globalNode = new(
            defaultEnvironment.Name,
            defaultEnvironment.IsTransient,
            Parent: null);
        RuntimeEnvironmentSeed globalSeed = new(
            defaultEnvironment.EnvironmentId,
            globalNode,
            [.. defaultEnvironment],
            RegisterName: false);
        RuntimeEnvironmentTransactionSeed environmentSeed = new(
            defaultEnvironment.EnvironmentId,
            [globalSeed]);
        TransactionRootSeed seed = new TransactionRootSeed().With(environments, environmentSeed);
        ExecutionTransaction transaction = coordinator.CreateRoot(seed);
        RuntimeEnvironmentContext environmentContext = RuntimeEnvironmentContext.CreateRoot(
            defaultEnvironment,
            environmentFactory,
            environments,
            transaction,
            loggerFactory);
        return new RootRuntimeState(transaction, environmentContext);
    }

    private sealed record RootRuntimeComposition(
        RootRuntimeState State,
        ModuleRuntimeOperations Operations);

    private sealed record RootRuntimeState(
        ExecutionTransaction Transaction,
        RuntimeEnvironmentContext EnvironmentContext);
}
