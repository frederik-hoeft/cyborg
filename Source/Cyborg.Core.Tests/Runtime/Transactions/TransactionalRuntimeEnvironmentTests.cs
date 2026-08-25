using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Modules.Runtime.Environments.Artifacts;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using Cyborg.Core.Modules.Runtime.Transactions;
using Cyborg.Core.Modules.Runtime.Transactions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime.Transactions;

[TestClass]
public sealed class TransactionalRuntimeEnvironmentTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void EnvironmentVariableParticipant_SiblingsUseStableBindingBaselineAndConflictingWritesFail()
    {
        EnvironmentVariableTransactionParticipant participant = new();
        RuntimeEnvironmentId environmentId = RuntimeEnvironmentId.Create();
        EnvironmentVariableStoreSeed[] seeds =
        [
            new(environmentId, [new KeyValuePair<string, object?>("value", 1)])
        ];
        TransactionRootSeed seed = new TransactionRootSeed().With(participant, seeds);
        ExecutionTransaction root = new TransactionCoordinator([participant]).CreateRoot(seed);
        ExecutionTransactionForkGroup fork = root.Fork();
        ExecutionTransaction first = fork.CreateChild();
        ExecutionTransaction second = fork.CreateChild();
        EnvironmentVariableTransactionState firstState = first.GetParticipantState(participant);
        EnvironmentVariableTransactionState secondState = second.GetParticipantState(participant);

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
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateArtifactCollection());
                case "child":
                    Assert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("temporary", out int temporary));
                    Assert.AreEqual(1, temporary);
                    Assert.IsTrue(runtime.GlobalEnvironment.TryRemoveVariable("temporary"));
                    runtime.GlobalEnvironment.SetVariable("child", "visible");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateArtifactCollection());
                case "named":
                    Assert.IsTrue(runtime.Environment.TryResolveVariable("value", out string? namedValue));
                    Assert.AreEqual("before", namedValue);
                    runtime.Environment.SetVariable("value", "after");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateArtifactCollection());
                case "failed":
                    runtime.GlobalEnvironment.SetVariable("failed-write", "committed");
                    return new EnvironmentProbeExecutionResult(module, ModuleExitStatus.Failed, runtime.Environment.CreateArtifactCollection());
                case "artifact":
                    IEnvironmentLike artifactValues = runtime.Environment.CreateArtifactCollection();
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
