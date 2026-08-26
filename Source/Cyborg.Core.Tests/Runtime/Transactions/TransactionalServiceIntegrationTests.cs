using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Cyborg.Core.Modules.Runtime.Transactions.Services;
using Cyborg.Core.Tests.TestInfrastructure;
using Cyborg.TestModules.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class TransactionalServiceIntegrationTests : CyborgCoreTestBase
{
    [TestMethod]
    public void JabProvider_ExternalTransactionalParticipantAndScopedFacade_Compose()
    {
        using TransactionalProbeServiceProvider services = new();
        TransactionalServiceParticipant[] participants = [.. services.GetServices<TransactionalServiceParticipant>()];
        RuntimeTransactionalServices transactionalServices = new(participants);
        ExecutionTransaction transaction = new TransactionCoordinator(transactionalServices.Participants).CreateRoot();
        using IServiceScope scope = services.CreateScope();
        transactionalServices.BindExecutionScope(scope.ServiceProvider, transaction);

        Assert.HasCount(1, participants);
        TransactionalServiceParticipant participant = participants[0];
        TransactionalProbeService probe = scope.ServiceProvider.GetRequiredService<TransactionalProbeService>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        Assert.IsInstanceOfType<TransactionalProbeParticipant>(participant);
        Assert.IsTrue(probe.IsAvailable);
        Assert.IsNotNull(runtime);
    }

    [TestMethod]
    public Task ResolveRuntime_ExternalTransactionalParticipantServiceModule_ComposesAsync() =>
        TestWithDIAsync(services => Assert.IsNotNull(services.GetRequiredService<IModuleRuntime>()));

    [TestMethod]
    public Task ExecuteAsync_NestedTransactionalServiceState_ComposesAndParentHandleObservesJoinAsync() =>
        TestWithDIAsync(async services =>
        {
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            TransactionalCounterRecorder recorder = services.GetRequiredService<TransactionalCounterRecorder>();
            ModuleReference root = CreateReference(ProbeBehavior.NestedRoot);

            IModuleExecutionResult result = await runtime.ExecuteAsync(root, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Success, result.Status);
            Assert.AreEqual(0, recorder.GetRequired("root-initial"));
            Assert.AreEqual(1, recorder.GetRequired("child-initial"));
            Assert.AreEqual(2, recorder.GetRequired("root-after-child"));
        }, ConfigureTransactionalServices);

    [TestMethod]
    public Task ResolveRuntime_TransactionalServiceStatePersistsWithinRootAndIsolatedAcrossRootsAsync() =>
        TestWithDIAsync(async services =>
        {
            IModuleRuntime firstRoot = services.GetRequiredService<IModuleRuntime>();
            IModuleRuntime secondRoot = services.GetRequiredService<IModuleRuntime>();
            TransactionalCounterRecorder recorder = services.GetRequiredService<TransactionalCounterRecorder>();

            await firstRoot.ExecuteAsync(CreateReference(ProbeBehavior.Increment), cancellationToken: TestContext.CancellationToken);
            await firstRoot.ExecuteAsync(CreateReference(ProbeBehavior.Increment), cancellationToken: TestContext.CancellationToken);
            await secondRoot.ExecuteAsync(CreateReference(ProbeBehavior.Increment), cancellationToken: TestContext.CancellationToken);

            IReadOnlyList<int> values = recorder.GetAll("increment-initial");
            Assert.HasCount(3, values);
            Assert.AreEqual(0, values[0]);
            Assert.AreEqual(1, values[1]);
            Assert.AreEqual(0, values[2]);
        }, ConfigureTransactionalServices);

    [TestMethod]
    public void TransactionCoordinator_CustomTransactionalServiceConflict_LeavesOwnerUnchanged()
    {
        TransactionalCounterParticipant descriptor = new();
        RuntimeTransactionalServices services = new([descriptor]);
        TransactionCoordinator coordinator = new(services.Participants);
        ExecutionTransaction root = coordinator.CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(first).Set(1);
        services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(second).Set(1);
        first.Complete();
        second.Complete();

        Assert.IsFalse(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNotNull(conflict);
        Assert.AreEqual(0, services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(root).Value);
    }

    [TestMethod]
    public void TransactionCoordinator_CustomTransactionalServiceConflict_DoesNotPublishBuiltInParticipantCandidate()
    {
        RuntimeModuleRegistryTransactionParticipant moduleRegistry = new();
        TransactionalCounterParticipant descriptor = new();
        RuntimeTransactionalServices services = new([descriptor]);
        TransactionCoordinator coordinator = new([moduleRegistry, .. services.Participants]);
        ExecutionTransaction root = coordinator.CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        ModuleContext module = new(
            CreateReference(ProbeBehavior.Increment),
            new ModuleEnvironment(),
            Configuration: null,
            ModuleRequirements.Default);
        Assert.IsTrue(first.GetParticipantState(moduleRegistry).TryAddModule("candidate", module));
        services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(first).Set(1);
        services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(second).Set(2);
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        Assert.IsFalse(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNotNull(conflict);
        Assert.IsInstanceOfType<TransactionalServiceParticipantAdapter>(conflict.Participant);
        Assert.IsFalse(root.GetParticipantState(moduleRegistry).TryGetModule("candidate", out ModuleContext? _));
        Assert.AreEqual(0, services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(root).Value);
    }

    [TestMethod]
    public void TransactionCoordinator_CustomTransactionalServiceConflict_UsesConfiguredResolver()
    {
        TransactionalCounterParticipant descriptor = new();
        RuntimeTransactionalServices services = new([descriptor]);
        TransactionCoordinator coordinator = new(services.Participants, new SelectLastContributorConflictStrategy());
        ExecutionTransaction root = coordinator.CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        fork.Continuation.Complete();
        services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(first).Set(1);
        services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(second).Set(2);
        first.Complete();
        second.Complete();

        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        Assert.AreEqual(2, services.GetState<TransactionalCounterParticipant, TransactionalCounterState>(root).Value);
    }

    [TestMethod]
    public void TransactionalServiceStateHandle_OwnerAccessWhileForkOpen_Fails()
    {
        TransactionalCounterParticipant descriptor = new();
        RuntimeTransactionalServices services = new([descriptor]);
        ExecutionTransaction root = new TransactionCoordinator(services.Participants).CreateRoot();
        TransactionalServiceContext context = new();
        ((ITransactionBoundTransactionalServiceContext)context).Bind(services, root);
        ITransactionalServiceState<TransactionalCounterState> state =
            context.GetState<TransactionalCounterParticipant, TransactionalCounterState>();
        ExecutionTransactionForkGroup fork = root.Fork();

        Assert.ThrowsExactly<InvalidOperationException>(() => state.Mutate(static counter => counter.Set(1)));

        fork.Discard();
    }

    [TestMethod]
    public void RuntimeTransactionalServices_DuplicateParticipantType_FailsExplicitly()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new RuntimeTransactionalServices([new TransactionalCounterParticipant(), new TransactionalCounterParticipant()]));
    }

    [TestMethod]
    public void RuntimeTransactionalServices_EmptyParticipantSet_BindsAvailableExecutionScopeContext()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddScoped<ITransactionalServiceContext, TransactionalServiceContext>();
        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        RuntimeTransactionalServices services = new([]);
        ExecutionTransaction root = new TransactionCoordinator(services.Participants).CreateRoot();
        services.BindExecutionScope(scope.ServiceProvider, root);
        ITransactionalServiceContext context = scope.ServiceProvider.GetRequiredService<ITransactionalServiceContext>();
        ITransactionalServiceState<TransactionalCounterState> state =
            context.GetState<TransactionalCounterParticipant, TransactionalCounterState>();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => state.Read(static counter => counter.Value));

        StringAssert.Contains(exception.Message, "is not registered with this execution runtime");
    }

    [TestMethod]
    public void TransactionCoordinator_CustomParticipantCannotFailWithoutReportingConflict()
    {
        InvalidFailureParticipant descriptor = new();
        RuntimeTransactionalServices services = new([descriptor]);
        ExecutionTransaction root = new TransactionCoordinator(services.Participants).CreateRoot();
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction child = fork.CreateChild();
        fork.Continuation.Complete();
        child.Complete();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            fork.TryJoin(out TransactionConflict? _));

        StringAssert.Contains(exception.Message, "without reporting a conflict");
        Assert.AreEqual(ExecutionTransactionForkLifecycle.Failed, fork.Lifecycle);
    }

    private static ModuleReference CreateReference(ProbeBehavior behavior) =>
        new(new TransactionalServiceProbeModule(behavior), TransactionalServiceProbeModule.MODULE_ID);

    private static void ConfigureTransactionalServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<TransactionalServiceParticipant, TransactionalCounterParticipant>();
        services.AddScoped<TransactionalCounterService>();
        services.AddSingleton<TransactionalCounterRecorder>();
        services.AddSingleton<IModuleWorkerFactory, TransactionalServiceProbeWorkerFactory>();
    }

    private enum ProbeBehavior
    {
        NestedRoot,
        Child,
        Increment
    }

    private sealed record TransactionalServiceProbeModule(ProbeBehavior Behavior) : ModuleBase
    {
        public const string MODULE_ID = "cyborg.tests.transactional-service.v1";
    }

    private sealed class TransactionalServiceProbeWorker(
        TransactionalServiceProbeModule module,
        TransactionalCounterService counter,
        TransactionalCounterRecorder recorder) : IModuleWorker
    {
        public string ModuleId => TransactionalServiceProbeModule.MODULE_ID;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            switch (module.Behavior)
            {
                case ProbeBehavior.NestedRoot:
                    recorder.Record("root-initial", counter.Value);
                    counter.Set(1);
                    await runtime.ExecuteAsync(CreateReference(ProbeBehavior.Child), cancellationToken: cancellationToken);
                    recorder.Record("root-after-child", counter.Value);
                    break;
                case ProbeBehavior.Child:
                    recorder.Record("child-initial", counter.Value);
                    counter.Set(2);
                    break;
                case ProbeBehavior.Increment:
                    int initial = counter.Value;
                    recorder.Record("increment-initial", initial);
                    counter.Set(initial + 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return new ProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
        }
    }

    private sealed class TransactionalServiceProbeWorkerFactory : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(moduleReference);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            return new TransactionalServiceProbeWorker(
                (TransactionalServiceProbeModule)moduleReference.Definition,
                serviceProvider.GetRequiredService<TransactionalCounterService>(),
                serviceProvider.GetRequiredService<TransactionalCounterRecorder>());
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            throw new NotSupportedException();

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class TransactionalCounterService(ITransactionalServiceContext context)
    {
        private readonly ITransactionalServiceState<TransactionalCounterState> _state =
            context.GetState<TransactionalCounterParticipant, TransactionalCounterState>();

        public int Value => _state.Read(static state => state.Value);

        public void Set(int value) => _state.Mutate(state => state.Set(value));
    }

    private sealed class TransactionalCounterParticipant : TransactionalServiceParticipant<TransactionalCounterState>
    {
        protected override TransactionalCounterState CreateRootState() => new(value: 0, isChanged: false);

        protected override TransactionalServiceFork<TransactionalCounterState> CreateFork(TransactionalCounterState ownerState) =>
            new TransactionalCounterFork(ownerState);
    }

    private sealed class TransactionalCounterFork(TransactionalCounterState ownerState) : TransactionalServiceFork<TransactionalCounterState>
    {
        private readonly TransactionalCounterState _ownerState = ownerState ?? throw new ArgumentNullException(nameof(ownerState));
        private readonly int _baselineValue = ownerState.Value;

        public override TransactionalCounterState CreateBranch() => new(_baselineValue, isChanged: false);

        public override bool TryPrepareMerge(
            IReadOnlyList<TransactionalCounterState> contributors,
            ITransactionalServiceConflictResolver conflictResolver,
            [NotNullWhen(true)] out TransactionalCounterState? candidate)
        {
            ArgumentNullException.ThrowIfNull(contributors);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            List<int> changedContributors = [];
            for (int i = 0; i < contributors.Count; i++)
            {
                if (contributors[i].IsChanged)
                {
                    changedContributors.Add(i);
                }
            }

            int value = _ownerState.Value;
            if (changedContributors.Count == 1)
            {
                value = contributors[changedContributors[0]].Value;
            }
            else if (changedContributors.Count > 1)
            {
                if (!conflictResolver.TryResolve(nameof(TransactionalCounterState.Value), changedContributors, out int selectedContributor))
                {
                    candidate = null;
                    return false;
                }
                value = contributors[selectedContributor].Value;
            }

            candidate = new TransactionalCounterState(value, _ownerState.IsChanged || changedContributors.Count > 0);
            return true;
        }
    }

    private sealed class InvalidFailureParticipant : TransactionalServiceParticipant<InvalidFailureState>
    {
        protected override InvalidFailureState CreateRootState() => new();

        protected override TransactionalServiceFork<InvalidFailureState> CreateFork(InvalidFailureState ownerState) => new InvalidFailureFork();
    }

    private sealed class InvalidFailureState;

    private sealed class InvalidFailureFork : TransactionalServiceFork<InvalidFailureState>
    {
        public override InvalidFailureState CreateBranch() => new();

        public override bool TryPrepareMerge(
            IReadOnlyList<InvalidFailureState> contributors,
            ITransactionalServiceConflictResolver conflictResolver,
            [NotNullWhen(true)] out InvalidFailureState? candidate)
        {
            candidate = null;
            return false;
        }
    }

    private sealed class TransactionalCounterState(int value, bool isChanged)
    {
        public int Value { get; private set; } = value;

        public bool IsChanged { get; private set; } = isChanged;

        public void Set(int value)
        {
            Value = value;
            IsChanged = true;
        }
    }

    private sealed class SelectLastContributorConflictStrategy : ITransactionConflictStrategy
    {
        public TransactionConflictResolution Resolve(TransactionConflict conflict)
        {
            ArgumentNullException.ThrowIfNull(conflict);
            return TransactionConflictResolution.UseContributor(conflict.ContributorIndices[^1]);
        }
    }

    private sealed class TransactionalCounterRecorder
    {
        private readonly Dictionary<string, List<int>> _values = [];

        public void Record(string key, int value)
        {
            lock (_values)
            {
                if (!_values.TryGetValue(key, out List<int>? values))
                {
                    values = [];
                    _values.Add(key, values);
                }
                values.Add(value);
            }
        }

        public int GetRequired(string key) => GetAll(key).Single();

        public IReadOnlyList<int> GetAll(string key)
        {
            lock (_values)
            {
                return _values.TryGetValue(key, out List<int>? values) ? [.. values] : [];
            }
        }
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
