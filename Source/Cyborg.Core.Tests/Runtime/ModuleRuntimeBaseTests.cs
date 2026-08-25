using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Microsoft.Extensions.DependencyInjection;
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
        globalEnvironment.SetVariable("cyborg.template.backup-job.docker.v1.container_name", "jellyfin");
        ProbeModuleWorker worker = new();
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new ProbeModuleWorkerFactory(worker))
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleContext moduleContext = new(
            Module: new ModuleReference(worker.Module, ProbeModule.ModuleId),
            Environment: ModuleEnvironment.Default,
            Configuration: null,
            Requires: new ModuleRequirements("cyborg.template.backup-job.docker.v1", ["container_name"]));

        IModuleExecutionResult executionResult = await runtime.ExecuteAsync(moduleContext, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, executionResult.Status);
        Assert.IsTrue(worker.SawContainerName);
        Assert.AreEqual("jellyfin", worker.ContainerName);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameLoadedReference_ActivatesFreshWorkersForConcurrentExecutionsAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RecordingProbeModuleWorkerFactory workerFactory = new();
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(workerFactory)
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference moduleReference = new(new ProbeModule(), ProbeModule.ModuleId);

        Task<IModuleExecutionResult> firstExecution = runtime.ExecuteAsync(moduleReference, cancellationToken: TestContext.CancellationToken);
        Task<IModuleExecutionResult> secondExecution = runtime.ExecuteAsync(moduleReference, cancellationToken: TestContext.CancellationToken);
        IModuleExecutionResult[] results = await Task.WhenAll(firstExecution, secondExecution);

        Assert.IsTrue(results.All(static result => result.Status == ModuleExitStatus.Success));
        Assert.HasCount(2, workerFactory.Workers);
        Assert.AreNotSame(workerFactory.Workers[0], workerFactory.Workers[1]);
    }

    [TestMethod]
    public void PrepareEnvironment_NamedEnvironment_IsResolvedThroughRuntimeCatalog()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory);
        ModuleEnvironment namedEnvironment = new()
        {
            Scope = EnvironmentScope.Isolated,
            Name = "named"
        };
        ModuleEnvironment reference = new()
        {
            Scope = EnvironmentScope.Reference,
            Name = "named"
        };

        IRuntimeEnvironment created = runtime.PrepareEnvironment(namedEnvironment);
        IRuntimeEnvironment resolved = runtime.PrepareEnvironment(reference);

        Assert.AreSame(created, resolved);
    }

    [TestMethod]
    public void PublicRuntimeSurface_DoesNotExposeEnvironmentCatalogMutation()
    {
        string[] methodNames = [.. typeof(IModuleRuntime).GetMethods().Select(static method => method.Name)];

        Assert.DoesNotContain("TryAddEnvironment", methodNames);
        Assert.DoesNotContain("TryGetEnvironment", methodNames);
        Assert.DoesNotContain("TryRemoveEnvironment", methodNames);
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

    private sealed class ProbeModuleWorkerFactory(ProbeModuleWorker worker) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider) => worker;

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule => worker;

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule => worker;
    }

    private sealed class RecordingProbeModuleWorkerFactory : IModuleWorkerFactory
    {
        private readonly List<ProbeModuleWorker> _workers = [];

        public IReadOnlyList<ProbeModuleWorker> Workers => _workers;

        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            ProbeModuleWorker worker = new();
            lock (_workers)
            {
                _workers.Add(worker);
            }
            return worker;
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            throw new NotSupportedException();

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule => throw new NotSupportedException();
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
