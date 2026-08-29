using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Environments.Artifacts;
using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using Cyborg.Core.Runtime.Engine.Transactions;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class TransactionalRuntimeEnvironmentTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void RuntimeEnvironmentParticipant_SiblingsUseStableBindingBaselineAndConflictingWritesFail()
    {
        RuntimeEnvironmentTransactionParticipant participant = new();
        RuntimeEnvironmentId environmentId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode node = new("global", IsTransient: false, Parent: null);
        RuntimeEnvironmentSeed environmentSeed = new(
            environmentId,
            node,
            [KeyValuePair.Create("value", (object?)1)],
            RegisterName: false);
        RuntimeEnvironmentTransactionSeed environmentRootSeed = new(environmentId, [environmentSeed]);
        TransactionRootSeed seed = new TransactionRootSeed().With(participant, environmentRootSeed);
        ModuleTransaction root = new TransactionCoordinator([participant]).CreateRoot(seed);
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        RuntimeEnvironmentTransactionState firstState = first.GetParticipantState(participant);
        RuntimeEnvironmentTransactionState secondState = second.GetParticipantState(participant);

        Assert.IsTrue(firstState.TryGetValue(environmentId, "value", out object? firstBaseline));
        Assert.AreEqual(1, firstBaseline);
        firstState.SetValue(environmentId, "value", 2);
        Assert.IsTrue(secondState.TryGetValue(environmentId, "value", out object? secondBaseline));
        Assert.AreEqual(1, secondBaseline);
        secondState.SetValue(environmentId, "value", 3);
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        bool joined = fork.TryJoin(out TransactionConflict? conflict);

        Assert.IsFalse(joined);
        Assert.IsNotNull(conflict);
        Assert.AreEqual(new EnvironmentVariableBinding(environmentId, "value"), conflict.LogicalKey);
        Assert.IsTrue(root.GetParticipantState(participant).TryGetValue(environmentId, "value", out object? rootValue));
        Assert.AreEqual(1, rootValue);
    }

    [TestMethod]
    public void RuntimeEnvironmentParticipant_RegistrationCollisionChangesNeitherCatalogNorTopology()
    {
        RuntimeEnvironmentTransactionParticipant participant = new();
        ModuleTransaction root = CreateEnvironmentRoot(participant, out RuntimeEnvironmentId _);
        RuntimeEnvironmentTransactionState state = root.GetParticipantState(participant);
        RuntimeEnvironmentId firstId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentId secondId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode firstNode = new("named", IsTransient: false, Parent: null);
        RuntimeEnvironmentNode secondNode = new("named", IsTransient: false, Parent: null);

        Assert.IsTrue(state.TryAddNamedEnvironment(firstId, firstNode, values: []));
        Assert.IsFalse(state.TryAddNamedEnvironment(secondId, secondNode, values: []));

        Assert.IsTrue(state.TryGetRegisteredEnvironment("named", out RuntimeEnvironmentId registeredId));
        Assert.AreEqual(firstId, registeredId);
        Assert.IsTrue(state.ContainsEnvironment(firstId));
        Assert.IsFalse(state.ContainsEnvironment(secondId));
    }

    [TestMethod]
    public void RuntimeEnvironmentParticipant_TransientChildEnvironmentIsPrunedOnJoin()
    {
        RuntimeEnvironmentTransactionParticipant participant = new();
        ModuleTransaction root = CreateEnvironmentRoot(participant, out RuntimeEnvironmentId _);
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        RuntimeEnvironmentTransactionState childState = child.GetParticipantState(participant);
        RuntimeEnvironmentId transientId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode transientNode = new("temporary", IsTransient: true, Parent: null);
        childState.AddEnvironment(transientId, transientNode, [new KeyValuePair<string, object?>("value", 42)]);
        childState.SetValue(transientId, "updated", "discarded");
        fork.Continuation.Complete();
        child.Complete();

        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        RuntimeEnvironmentTransactionState rootState = root.GetParticipantState(participant);
        Assert.IsFalse(rootState.ContainsEnvironment(transientId));
        Assert.IsFalse(rootState.TryGetValue(transientId, "value", out object? _));
        Assert.IsFalse(rootState.TryGetValue(transientId, "updated", out object? _));
    }

    [TestMethod]
    public void RuntimeEnvironmentParticipant_NamedEnvironmentRetainsTransientAncestorOnJoin()
    {
        RuntimeEnvironmentTransactionParticipant participant = new();
        ModuleTransaction root = CreateEnvironmentRoot(participant, out RuntimeEnvironmentId _);
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction child = fork.CreateChild();
        RuntimeEnvironmentTransactionState childState = child.GetParticipantState(participant);
        RuntimeEnvironmentId transientParentId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentId namedChildId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode transientParent = new("temporary-parent", IsTransient: true, Parent: null);
        RuntimeEnvironmentParent parentReference = new(transientParentId, Namespace: "parent", OverrideResolutionTags: []);
        RuntimeEnvironmentNode namedChild = new("named-child", IsTransient: false, parentReference);
        childState.AddEnvironment(
            transientParentId,
            transientParent,
            [new KeyValuePair<string, object?>("inherited", "parent")]);
        Assert.IsTrue(childState.TryAddNamedEnvironment(
            namedChildId,
            namedChild,
            [new KeyValuePair<string, object?>("local", "child")]));
        fork.Continuation.Complete();
        child.Complete();

        Assert.IsTrue(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNull(conflict);
        RuntimeEnvironmentTransactionState rootState = root.GetParticipantState(participant);
        Assert.IsTrue(rootState.ContainsEnvironment(transientParentId));
        Assert.IsTrue(rootState.ContainsEnvironment(namedChildId));
        Assert.IsTrue(rootState.TryGetRegisteredEnvironment("named-child", out RuntimeEnvironmentId registeredId));
        Assert.AreEqual(namedChildId, registeredId);
        Assert.IsTrue(rootState.TryGetValue(transientParentId, "inherited", out object? inherited));
        Assert.AreEqual("parent", inherited);
        Assert.IsTrue(rootState.TryGetValue(namedChildId, "local", out object? local));
        Assert.AreEqual("child", local);
    }

    [TestMethod]
    public void RuntimeEnvironmentParticipant_SiblingNamedRegistrationsConflictAtomically()
    {
        RuntimeEnvironmentTransactionParticipant participant = new();
        ModuleTransaction root = CreateEnvironmentRoot(participant, out RuntimeEnvironmentId _);
        ModuleTransactionForkGroup fork = root.Fork();
        ModuleTransaction first = fork.CreateChild();
        ModuleTransaction second = fork.CreateChild();
        RuntimeEnvironmentTransactionState firstState = first.GetParticipantState(participant);
        RuntimeEnvironmentTransactionState secondState = second.GetParticipantState(participant);
        RuntimeEnvironmentId firstId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentId secondId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode firstNode = new("shared", IsTransient: false, Parent: null);
        RuntimeEnvironmentNode secondNode = new("shared", IsTransient: false, Parent: null);
        Assert.IsTrue(firstState.TryAddNamedEnvironment(firstId, firstNode, [new KeyValuePair<string, object?>("value", "first")]));
        Assert.IsFalse(secondState.TryGetRegisteredEnvironment("shared", out RuntimeEnvironmentId _));
        RuntimeEnvironmentTransactionState continuationState = fork.Continuation.GetParticipantState(participant);
        Assert.IsFalse(continuationState.TryGetRegisteredEnvironment("shared", out RuntimeEnvironmentId _));
        Assert.IsTrue(secondState.TryAddNamedEnvironment(secondId, secondNode, [new KeyValuePair<string, object?>("value", "second")]));
        fork.Continuation.Complete();
        first.Complete();
        second.Complete();

        Assert.IsFalse(fork.TryJoin(out TransactionConflict? conflict));
        Assert.IsNotNull(conflict);
        Assert.AreSame(participant, conflict.Participant);
        RuntimeEnvironmentTransactionState rootState = root.GetParticipantState(participant);
        Assert.IsFalse(rootState.TryGetRegisteredEnvironment("shared", out RuntimeEnvironmentId _));
        Assert.IsFalse(rootState.ContainsEnvironment(firstId));
        Assert.IsFalse(rootState.ContainsEnvironment(secondId));
    }

    [TestMethod]
    public async Task ExecuteAsync_NestedEnvironmentChangesComposeIntoRootTransactionAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new EnvironmentProbeWorkerFactory())
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference rootModule = new(new EnvironmentProbeModule { Name = "root" }, EnvironmentProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(rootModule, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("temporary", out object? _));
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("child", out string? child));
        Assert.AreEqual("visible", child);
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("after-child", out int afterChild));
        Assert.AreEqual(2, afterChild);
    }

    [TestMethod]
    public async Task ExecuteAsync_NamedEnvironmentBindingUsesSameLogicalStoreAcrossTransactionAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new EnvironmentProbeWorkerFactory())
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        IRuntimeEnvironment namedEnvironment = runtime.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "named"
        });
        namedEnvironment.SetVariable("value", "before");
        ModuleReference module = new(new EnvironmentProbeModule { Name = "named" }, EnvironmentProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(module, namedEnvironment, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.IsTrue(namedEnvironment.TryResolveVariable("value", out string? value));
        Assert.AreEqual("after", value);
        IRuntimeEnvironment resolved = runtime.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Reference,
            Name = "named"
        });
        Assert.IsTrue(resolved.TryResolveVariable("value", out string? resolvedValue));
        Assert.AreEqual("after", resolvedValue);
    }

    [TestMethod]
    public async Task ExecuteAsync_NestedNamedEnvironmentTopologyReconcilesToCallerAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new EnvironmentProbeWorkerFactory())
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference module = new(new EnvironmentProbeModule { Name = "topology-root" }, EnvironmentProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        IRuntimeEnvironment resolved = runtime.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Reference,
            Name = "nested-named"
        });
        Assert.IsTrue(resolved.TryResolveVariable("local", out string? local));
        Assert.AreEqual("child", local);
        Assert.IsTrue(resolved.TryResolveVariable("inherited", out string? inherited));
        Assert.AreEqual("parent", inherited);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailedResultStillReconcilesEnvironmentChangesAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new EnvironmentProbeWorkerFactory())
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference module = new(new EnvironmentProbeModule { Name = "failed" }, EnvironmentProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Failed, result.Status);
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("failed-write", out string? value));
        Assert.AreEqual("committed", value);
    }

    [TestMethod]
    public async Task Exit_DefaultParentArtifactsPublishThroughCurrentTransactionAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new EnvironmentProbeWorkerFactory())
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleArtifacts artifacts = ModuleArtifacts.Default with
        {
            Environment = ArtifactModuleEnvironment.Default
        };
        ModuleReference module = new(
            new EnvironmentProbeModule { Name = "artifact", Artifacts = artifacts },
            EnvironmentProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("artifact-value", out int value));
        Assert.AreEqual(42, value);
    }

    private static ModuleTransaction CreateEnvironmentRoot(
        RuntimeEnvironmentTransactionParticipant participant,
        out RuntimeEnvironmentId globalEnvironmentId)
    {
        ArgumentNullException.ThrowIfNull(participant);
        globalEnvironmentId = RuntimeEnvironmentId.Create();
        RuntimeEnvironmentNode globalNode = new(
            "global",
            IsTransient: false,
            Parent: null);
        RuntimeEnvironmentSeed environmentSeed = new(
            globalEnvironmentId,
            globalNode,
            Values: [],
            RegisterName: false);
        RuntimeEnvironmentTransactionSeed rootSeed = new(globalEnvironmentId, [environmentSeed]);
        TransactionRootSeed seed = new TransactionRootSeed().With(participant, rootSeed);
        return new TransactionCoordinator([participant]).CreateRoot(seed);
    }

    private sealed record EnvironmentProbeModule : ModuleBase, IModuleDefinition
    {
        public static string ModuleId => "cyborg.tests.transaction-environment-probe.v1";
    }

    private sealed class EnvironmentProbeWorkerFactory : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider) =>
            new EnvironmentProbeWorker((EnvironmentProbeModule)moduleReference.Definition);

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            throw new NotSupportedException();

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class EnvironmentProbeWorker(EnvironmentProbeModule module) : IModuleWorker
    {
        public string ModuleId => EnvironmentProbeModule.ModuleId;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            switch (module.Name)
            {
                case "root":
                    runtime.GlobalEnvironment.SetVariable("temporary", 1);
                    ModuleReference child = new(new EnvironmentProbeModule { Name = "child" }, EnvironmentProbeModule.ModuleId);
                    IModuleExecutionResult childResult = await runtime.ExecuteAsync(child, runtime.Environment, cancellationToken);
                    Assert.AreEqual(ModuleExitStatus.Success, childResult.Status);
                    Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("temporary", out object? _));
                    Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("child", out string? childValue));
                    Assert.AreEqual("visible", childValue);
                    runtime.GlobalEnvironment.SetVariable("after-child", 2);
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
                case "child":
                    Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("temporary", out int temporary));
                    Assert.AreEqual(1, temporary);
                    Assert.IsTrue(runtime.GlobalEnvironment.TryRemoveVariable("temporary"));
                    runtime.GlobalEnvironment.SetVariable("child", "visible");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
                case "named":
                    Assert.IsTrue(runtime.Environment.TryResolveVariable("value", out string? namedValue));
                    Assert.AreEqual("before", namedValue);
                    runtime.Environment.SetVariable("value", "after");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
                case "topology-root":
                    IRuntimeEnvironment transientParent = runtime.PrepareEnvironment(new ModuleEnvironment
                    {
                        Scope = EnvironmentScope.Isolated,
                        Name = "transient-parent",
                        Transient = true
                    });
                    transientParent.SetVariable("inherited", "parent");
                    ModuleReference topologyChild = new(
                        new EnvironmentProbeModule { Name = "topology-child" },
                        EnvironmentProbeModule.ModuleId);
                    IModuleExecutionResult topologyResult = await runtime.ExecuteAsync(
                        topologyChild,
                        transientParent,
                        cancellationToken);
                    Assert.AreEqual(ModuleExitStatus.Success, topologyResult.Status);
                    IRuntimeEnvironment nestedNamed = runtime.PrepareEnvironment(new ModuleEnvironment
                    {
                        Scope = EnvironmentScope.Reference,
                        Name = "nested-named"
                    });
                    Assert.IsTrue(nestedNamed.TryResolveVariable("local", out string? nestedLocal));
                    Assert.AreEqual("child", nestedLocal);
                    Assert.IsTrue(nestedNamed.TryResolveVariable("inherited", out string? nestedInherited));
                    Assert.AreEqual("parent", nestedInherited);
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
                case "topology-child":
                    IRuntimeEnvironment namedChild = runtime.PrepareEnvironment(new ModuleEnvironment
                    {
                        Scope = EnvironmentScope.InheritParent,
                        Name = "nested-named"
                    });
                    namedChild.SetVariable("local", "child");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
                case "failed":
                    runtime.GlobalEnvironment.SetVariable("failed-write", "committed");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Failed, runtime.Environment.CreateTestArtifactCollection());
                case "artifact":
                    IEnvironmentLike artifactValues = runtime.Environment.CreateTestArtifactCollection();
                    artifactValues.SetVariable("artifact-value", 42);
                    ProbeArtifactsBuilder artifactBuilder = new(artifactValues);
                    return runtime.Exit(new TypedEnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, artifactBuilder));
                default:
                    throw new InvalidOperationException($"Unsupported environment probe operation '{module.Name}'.");
            }
        }
    }

    private sealed class ProbeArtifactsBuilder(IEnvironmentLike artifacts) : IModuleArtifactsBuilder
    {
        public VariableSyntaxBuilder SyntaxFactory => artifacts.SyntaxFactory;

        public string Namespace => artifacts.Namespace;

        public IModuleArtifactsBuilder Expose(string path, object? artifact)
        {
            artifacts.SetVariable(path, artifact);
            return this;
        }

        public IModuleArtifactsBuilder Expose(string ns, string name, object? artifact)
        {
            artifacts.SetVariable(SyntaxFactory.Path(ns, name), artifact);
            return this;
        }

        public IModuleArtifactsBuilder Expose<T>(T artifact) where T : class, IDecomposable => throw new NotSupportedException();

        public IModuleArtifactsBuilder Expose<T>(string path, T artifact) where T : class, IDecomposable => throw new NotSupportedException();

        IEnvironmentLike IModuleArtifactsBuilder.Build(ModuleExitStatus exitStatus) => artifacts;
    }

    private sealed record EnvironmentProbeExecutionResult(
        IModule Module,
        ModuleExitStatus Status,
        IVariableResolverScope Artifacts) : IModuleExecutionResult;

    private sealed record TypedEnvironmentProbeExecutionResult(
        EnvironmentProbeModule Module,
        ModuleExitStatus Status,
        IModuleArtifactsBuilder Artifacts) : IModuleExecutionResult<EnvironmentProbeModule>;
}
