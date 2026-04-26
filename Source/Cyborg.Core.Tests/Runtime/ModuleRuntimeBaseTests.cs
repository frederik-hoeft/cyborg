using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ModuleRuntimeBaseTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_WithNamespacedRequiredArguments_ImportsArgumentsIntoChildEnvironmentUnqualifiedAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory);
        globalEnvironment.SetVariable("cyborg.template.backup-job.docker.v1.container_name", "jellyfin");
        ProbeModuleWorker worker = new();
        ModuleContext moduleContext = new(
            Module: new ModuleReference(worker),
            Environment: ModuleEnvironment.Default,
            Configuration: null,
            Requires: new ModuleRequirements("cyborg.template.backup-job.docker.v1", ["container_name"]));

        IModuleExecutionResult executionResult = await runtime.ExecuteAsync(moduleContext, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, executionResult.Status);
        Assert.IsTrue(worker.SawContainerName);
        Assert.AreEqual("jellyfin", worker.ContainerName);
    }

    private sealed class ProbeModuleWorker : IModuleWorker
    {
        public string ModuleId => ProbeModule.ModuleId;

        public IModule Module { get; } = new ProbeModule();

        public bool SawContainerName { get; private set; }

        public string? ContainerName { get; private set; }

        Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            SawContainerName = runtime.Environment.TryResolveVariable("container_name", out string? containerName);
            ContainerName = containerName;
            return Task.FromResult<IModuleExecutionResult>(new ProbeExecutionResult((ProbeModule)Module, ModuleExitStatus.Success, runtime.Environment.CreateArtifactCollection()));
        }
    }

    private sealed record ProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.probe.v1";
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
