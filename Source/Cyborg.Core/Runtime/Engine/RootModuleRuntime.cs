using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Services.Transactions;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

internal sealed class RootModuleRuntime : ModuleRuntimeBase
{
    protected override IModuleRuntime Root => this;

    protected override IModuleRuntime? Parent => null;

    public RootModuleRuntime(GlobalRuntimeEnvironment defaultEnvironment, ILoggerFactory loggerFactory, IServiceProvider? serviceProvider = null)
        : this(CreateStandaloneComposition(defaultEnvironment, loggerFactory), serviceProvider)
    {
    }

    internal RootModuleRuntime(
        GlobalRuntimeEnvironment defaultEnvironment,
        IRuntimeEnvironmentFactory environmentFactory,
        ModuleRuntimeServices operations,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
        : this(CreateState(defaultEnvironment, environmentFactory, operations, loggerFactory), operations, serviceProvider)
    {
    }

    private RootModuleRuntime(RootRuntimeComposition composition, IServiceProvider? serviceProvider)
        : this(composition.State, composition.Operations, serviceProvider)
    {
    }

    private RootModuleRuntime(RootRuntimeState state, ModuleRuntimeServices operations, IServiceProvider? serviceProvider)
        : base(state.EnvironmentContext, operations, state.Transaction, serviceProvider)
    {
    }

    private static RootRuntimeComposition CreateStandaloneComposition(GlobalRuntimeEnvironment defaultEnvironment, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        IRuntimeEnvironmentFactory environmentFactory = new DefaultRuntimeEnvironmentFactory(defaultEnvironment.SyntaxFactory, taggedStringConversionObserver: null);
        IRuntimeModuleRegistry moduleRegistry = new RuntimeModuleRegistry();
        ModuleRuntimeServices operations = new(
            new ModuleArtifactPublisher(loggerFactory),
            new ModuleContextRunner(defaultEnvironment.SyntaxFactory, environmentFactory, loggerFactory),
            new ModuleDispatcher(environmentFactory, loggerFactory),
            moduleRegistry,
            new RuntimeTransactionalServices([]));
        return new RootRuntimeComposition(CreateState(defaultEnvironment, environmentFactory, operations, loggerFactory), operations);
    }

    private static RootRuntimeState CreateState(GlobalRuntimeEnvironment defaultEnvironment, IRuntimeEnvironmentFactory environmentFactory, ModuleRuntimeServices operations, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);
        ArgumentNullException.ThrowIfNull(environmentFactory);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        RuntimeEnvironmentTransactionParticipant environments = new();
        TransactionCoordinator coordinator = new([environments, operations.ModuleRegistry.Participant, .. operations.Transactional.Participants]);
        RuntimeEnvironmentNode globalNode = new(defaultEnvironment.Name, defaultEnvironment.IsTransient, Parent: null);
        RuntimeEnvironmentSeed globalSeed = new(defaultEnvironment.EnvironmentId, globalNode, [.. defaultEnvironment], RegisterName: false);
        RuntimeEnvironmentTransactionSeed environmentSeed = new(defaultEnvironment.EnvironmentId, [globalSeed]);
        TransactionRootSeed seed = new TransactionRootSeed().With(environments, environmentSeed);
        ModuleTransaction transaction = coordinator.CreateRoot(seed);
        RuntimeEnvironmentContext environmentContext = RuntimeEnvironmentContext.CreateRoot(defaultEnvironment, environmentFactory, environments, transaction, loggerFactory);
        return new RootRuntimeState(transaction, environmentContext);
    }

    private sealed record RootRuntimeComposition(RootRuntimeState State, ModuleRuntimeServices Operations);

    private sealed record RootRuntimeState(ModuleTransaction Transaction, RuntimeEnvironmentContext EnvironmentContext);
}
