using System.Diagnostics.CodeAnalysis;
using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Runtime.Services.Validation;
using Cyborg.Core.Services.Default;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Core.Tests.Debugging;

[TestClass]
public sealed class WorkflowDebuggerRuntimeIntegrationTests : CyborgCoreTestBase
{
    protected override void BuildConfiguration(IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IServiceSelectionKey<IDebugFrontend> frontendKey = configuration.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
        configuration.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "test"));
    }

    [TestMethod]
    public Task Test_RuntimeScopedBranchControl_StepReconcilesIntoNextInvocationAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IDebugFrontend debugFrontend = services.GetRequiredService<IDebugFrontend>();
        Assert.IsInstanceOfType<StepThenContinueFrontend>(debugFrontend);
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)debugFrontend;
        int breakpointId = breakpoints.Add("^first$");

        IModuleExecutionResult first = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "first" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(breakpoints.Remove(breakpointId));
        IModuleExecutionResult second = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "second" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);
        IModuleExecutionResult third = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "third" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, first.Status);
        Assert.AreEqual(ModuleExitStatus.Success, second.Status);
        Assert.AreEqual(ModuleExitStatus.Success, third.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=first",
                "cyborg.tests.debug-orchestration.v1 name=second",
            ],
            frontend.Identities);
        Assert.AreEqual(0, breakpoints.Count);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Rollback_StepDecisionStillReachesTheNextInvocationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        ModuleReference failing = new(
            new DebugProbeModule
            {
                Name = "rollback-step",
                Transaction = new ModuleTransactionSettings(TransactionOnError.Rollback),
            },
            DebugProbeModule.ModuleId);

        IModuleExecutionResult failed = await runtime.ExecuteAsync(failing, cancellationToken: TestContext.CancellationToken);
        IModuleExecutionResult next = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "after-rollback" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Failed, failed.Status);
        Assert.AreEqual(ModuleExitStatus.Success, next.Status);
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("rolled-back", out object? _));
        Assert.AreSequenceEqual(["cyborg.tests.debug-orchestration.v1 name=after-rollback"], frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Rollback_NextDecisionStillReachesTheNextInvocationAsync() => TestWithDIAsync(async services =>
    {
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        frontend.Script = static (_, _) => DebugResumeAction.Continue;
        ModuleReference failing = new(
            new DebugProbeModule
            {
                Name = "rollback-next",
                Transaction = new ModuleTransactionSettings(TransactionOnError.Rollback),
            },
            DebugProbeModule.ModuleId);

        IModuleExecutionResult failed = await runtime.ExecuteAsync(failing, cancellationToken: TestContext.CancellationToken);
        IModuleExecutionResult next = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "after-rollback" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Failed, failed.Status);
        Assert.AreEqual(ModuleExitStatus.Success, next.Status);
        Assert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("rolled-back", out object? _));
        Assert.AreSequenceEqual(["cyborg.tests.debug-orchestration.v1 name=after-rollback"], frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Next_SkipsNestedChildAndPausesAtFollowingSiblingAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        breakpoints.Add("^lead$");
        frontend.Script = static (context, _) => context.ValidationResult.Module.Name switch
        {
            "lead" => DebugResumeAction.Next,
            _ => DebugResumeAction.Continue,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "next-parent" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=lead",
                "cyborg.tests.debug-orchestration.v1 name=sibling",
            ],
            frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Next_LastChildContinuesOnTheCallerAfterJoinAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        breakpoints.Add("^only$");
        frontend.Script = static (context, _) => context.ValidationResult.Module.Name switch
        {
            "only" => DebugResumeAction.Next,
            _ => DebugResumeAction.Continue,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "outer" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=only",
                "cyborg.tests.debug-orchestration.v1 name=after",
            ],
            frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Next_BreakpointInsideSubtreeStillPausesAndContinueClearsItAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        breakpoints.Add("^(over|child)$");
        frontend.Script = static (context, _) => context.ValidationResult.Module.Name switch
        {
            "over" => DebugResumeAction.Next,
            "child" => DebugResumeAction.Continue,
            _ => DebugResumeAction.Continue,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "over" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=over",
                "cyborg.tests.debug-orchestration.v1 name=child",
            ],
            frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Next_StepAtInnerBreakpointReplacesStepOverAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        breakpoints.Add("^(over|child)$");
        frontend.Script = static (context, _) => context.ValidationResult.Module.Name switch
        {
            "over" => DebugResumeAction.Next,
            "child" => DebugResumeAction.Step,
            _ => DebugResumeAction.Continue,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "over" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=over",
                "cyborg.tests.debug-orchestration.v1 name=child",
                "cyborg.tests.debug-orchestration.v1 name=grand",
            ],
            frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Next_ParallelSiblingDoesNotPauseAndJoinResumesOnTheParentAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        breakpoints.Add("^branch-a$");
        frontend.Script = static (context, _) => context.ValidationResult.Module.Name switch
        {
            "branch-a" => DebugResumeAction.Next,
            _ => DebugResumeAction.Continue,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "fanout" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=branch-a",
                "cyborg.tests.debug-orchestration.v1 name=after-fanout",
            ],
            frontend.Identities);
    }, ConfigureServices);

    [TestMethod]
    public Task Test_Next_DetachAtInnerBreakpointSuppressesPendingStepOverAsync() => TestWithDIAsync(async services =>
    {
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        StepThenContinueFrontend frontend = (StepThenContinueFrontend)services.GetRequiredService<IDebugFrontend>();
        breakpoints.Add("^(over|child)$");
        frontend.Script = static (context, _) => context.ValidationResult.Module.Name switch
        {
            "over" => DebugResumeAction.Next,
            "child" => DebugResumeAction.Detach,
            _ => DebugResumeAction.Continue,
        };

        IModuleExecutionResult result = await runtime.ExecuteAsync(
            new ModuleReference(new DebugProbeModule { Name = "over" }, DebugProbeModule.ModuleId),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreSequenceEqual(
            [
                "cyborg.tests.debug-orchestration.v1 name=over",
                "cyborg.tests.debug-orchestration.v1 name=child",
            ],
            frontend.Identities);
        Assert.AreEqual(0, breakpoints.Count);
    }, ConfigureServices);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<IDebugFrontend, StepThenContinueFrontend>();
        services.AddSingleton<IModuleWorkerFactory, DebugProbeWorkerFactory>();
    }

    private static ModuleArtifacts PreparedArtifacts { get; } = ModuleArtifacts.Default with
    {
        Environment = ArtifactModuleEnvironment.Default,
    };

    private sealed record DebugProbeModule : ModuleBase, IModule<DebugProbeModule>
    {
        public static string ModuleId => "cyborg.tests.debug-orchestration.v1";

        public ValueTask<IValidationResult<DebugProbeModule>> ValidateAsync(
            IModuleRuntime runtime,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(ValidationResult.Valid(this with { Artifacts = PreparedArtifacts }));
    }

    private sealed class DebugProbeWorkerFactory : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider) =>
            new DebugProbeWorker(new DefaultWorkerContext<DebugProbeModule>((DebugProbeModule)moduleReference.Definition, serviceProvider));

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class DebugProbeWorker(IWorkerContext<DebugProbeModule> context) : ModuleWorker<DebugProbeModule>(context)
    {
        protected override async Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            switch (Module.Name)
            {
                case "rollback-step":
                    ServiceProvider.GetRequiredService<IDebugBranchControl>().Step();
                    runtime.GlobalEnvironment.SetVariable("rolled-back", "nope");
                    return runtime.Exit(Failed());
                case "rollback-next":
                {
                    IModuleExecutionRuntime executionRuntime = (IModuleExecutionRuntime)runtime;
                    ModuleExecutionId anchor = executionRuntime.InvocationContext?.ExecutionId
                        ?? throw new InvalidOperationException("A rollback next probe requires an execution id.");
                    ServiceProvider.GetRequiredService<IDebugBranchControl>().Next(anchor);
                    runtime.GlobalEnvironment.SetVariable("rolled-back", "nope");
                    return runtime.Exit(Failed());
                }
                case "next-parent":
                    await runtime.ExecuteAsync(Probe("lead"), cancellationToken: cancellationToken);
                    await runtime.ExecuteAsync(Probe("sibling"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                case "lead":
                    await runtime.ExecuteAsync(Probe("nested"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                case "outer":
                    await runtime.ExecuteAsync(Probe("inner"), cancellationToken: cancellationToken);
                    await runtime.ExecuteAsync(Probe("after"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                case "inner":
                    await runtime.ExecuteAsync(Probe("only"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                case "over":
                    await runtime.ExecuteAsync(Probe("child"), cancellationToken: cancellationToken);
                    await runtime.ExecuteAsync(Probe("after-over"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                case "child":
                    await runtime.ExecuteAsync(Probe("grand"), cancellationToken: cancellationToken);
                    await runtime.ExecuteAsync(Probe("later"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                case "fanout":
                    await runtime.ExecuteConcurrentlyAsync([ProbeContext("branch-a"), ProbeContext("branch-b")], cancellationToken);
                    await runtime.ExecuteAsync(Probe("after-fanout"), cancellationToken: cancellationToken);
                    return runtime.Exit(Success());
                default:
                    return runtime.Exit(Success());
            }
        }

        private static ModuleReference Probe(string name) => new(new DebugProbeModule { Name = name }, DebugProbeModule.ModuleId);

        private static ModuleContext ProbeContext(string name) => new(Probe(name), ModuleEnvironment.Default, Configuration: null, ModuleRequirements.Default);
    }

    private sealed class StepThenContinueFrontend : IDebugFrontend
    {
        public string Key => "test";

        public List<string> Identities { get; } = [];

        public Func<IDebugPauseContext, int, DebugResumeAction>? Script { get; set; }

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = Identities.Count;
            Identities.Add(context.GetModuleIdentity());
            DebugResumeAction action = Script is null
                ? index == 0 ? DebugResumeAction.Step : DebugResumeAction.Continue
                : Script(context, index);
            return ValueTask.FromResult(action);
        }
    }
}
