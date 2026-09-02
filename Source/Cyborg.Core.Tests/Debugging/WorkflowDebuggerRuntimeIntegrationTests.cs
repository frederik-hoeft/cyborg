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
        protected override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken) =>
            Task.FromResult(runtime.Exit(Success()));
    }

    private sealed class StepThenContinueFrontend : IDebugFrontend
    {
        public string Key => "test";

        public List<string> Identities { get; } = [];

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Identities.Add(context.GetModuleIdentity());
            DebugResumeAction action = Identities.Count == 1 ? DebugResumeAction.Step : DebugResumeAction.Continue;
            return ValueTask.FromResult(action);
        }
    }
}
