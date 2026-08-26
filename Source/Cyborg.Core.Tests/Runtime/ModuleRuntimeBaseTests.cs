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
    public async Task ExecuteAsync_NestedInvocations_CreateIndependentScopesAndShareSingletonsAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        ScopeProbeRecorder recorder = new();
        ScopeProbeWorkerFactory workerFactory = new(recorder);
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddScoped<ScopedExecutionProbe>()
            .AddSingleton<SingletonExecutionProbe>()
            .AddSingleton<IModuleWorkerFactory>(workerFactory)
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference rootModule = new(new ProbeModule { Name = "root" }, ProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(rootModule, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        ScopeProbeRecord root = recorder.GetRequired("root");
        ScopeProbeRecord child = recorder.GetRequired("child");
        ScopeProbeRecord grandchild = recorder.GetRequired("grandchild");
        ScopeProbeRecord sibling = recorder.GetRequired("sibling");
        Assert.AreNotEqual(root.ScopedProbe.Id, child.ScopedProbe.Id);
        Assert.AreNotEqual(root.ScopedProbe.Id, sibling.ScopedProbe.Id);
        Assert.AreNotEqual(child.ScopedProbe.Id, grandchild.ScopedProbe.Id);
        Assert.AreNotEqual(child.ScopedProbe.Id, sibling.ScopedProbe.Id);
        Assert.AreSame(root.SingletonProbe, child.SingletonProbe);
        Assert.AreSame(root.SingletonProbe, grandchild.SingletonProbe);
        Assert.AreSame(root.SingletonProbe, sibling.SingletonProbe);
        Assert.IsTrue(recorder.AllScopesAliveDuringExecution);
        Assert.IsTrue(recorder.Records.All(static record => record.ScopedProbe.IsDisposed));
    }

    [TestMethod]
    public async Task ExecuteAsync_ModuleContext_ConfigurationUsesNestedScopeAndMainUsesOwningScopeAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        ScopeProbeRecorder recorder = new();
        ScopeProbeWorkerFactory workerFactory = new(recorder);
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddScoped<ScopedExecutionProbe>()
            .AddSingleton<SingletonExecutionProbe>()
            .AddSingleton<IModuleWorkerFactory>(workerFactory)
            .BuildServiceProvider();
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference configurationReference = new(new ProbeModule { Name = "configuration" }, ProbeModule.ModuleId);
        ModuleReference mainReference = new(new ProbeModule { Name = "main" }, ProbeModule.ModuleId);
        ModuleContext moduleContext = new(mainReference, ModuleEnvironment.Default, configurationReference, ModuleRequirements.Default);

        IModuleExecutionResult result = await runtime.ExecuteAsync(moduleContext, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        ScopeProbeRecord configurationRecord = recorder.GetRequired("configuration");
        ScopeProbeRecord mainRecord = recorder.GetRequired("main");
        Assert.AreNotEqual(configurationRecord.ScopedProbe.Id, mainRecord.ScopedProbe.Id);
        Assert.AreSame(configurationRecord.SingletonProbe, mainRecord.SingletonProbe);
        Assert.IsTrue(configurationRecord.ScopedProbe.IsDisposed);
        Assert.IsTrue(mainRecord.ScopedProbe.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServiceProviderOnlyResolvesScopeFactory_StillCreatesExecutionScopeAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        ProbeModuleWorker worker = new();
        using ServiceProvider innerProvider = new ServiceCollection()
            .AddSingleton<IModuleWorkerFactory>(new ProbeModuleWorkerFactory(worker))
            .BuildServiceProvider();
        DelegatingServiceProvider serviceProvider = new(innerProvider);
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, serviceProvider);
        ModuleReference moduleReference = new(worker.Module, ProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(moduleReference, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServiceProviderCannotCreateScope_FailsExplicitlyAsync()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory, new NullServiceProvider());
        ModuleReference moduleReference = new(new ProbeModule(), ProbeModule.ModuleId);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            runtime.ExecuteAsync(moduleReference, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public void PrepareEnvironment_NamedEnvironment_ResolvesSameLogicalEnvironment()
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
        created.SetVariable("value", 42);
        IRuntimeEnvironment resolved = runtime.PrepareEnvironment(reference);

        Assert.AreEqual(created.Name, resolved.Name);
        Assert.IsTrue(resolved.TryResolveVariable("value", out int value));
        Assert.AreEqual(42, value);
        resolved.SetVariable("other", "shared");
        Assert.IsTrue(created.TryResolveVariable("other", out string? other));
        Assert.AreEqual("shared", other);
    }

    [TestMethod]
    public void PrepareEnvironment_DuplicateNamedEnvironmentFailsWithoutReplacingRegistration()
    {
        GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static _ => { });
        RootModuleRuntime runtime = new(globalEnvironment, loggerFactory);
        ModuleEnvironment namedEnvironment = new()
        {
            Scope = EnvironmentScope.Isolated,
            Name = "named"
        };

        IRuntimeEnvironment first = runtime.PrepareEnvironment(namedEnvironment);
        first.SetVariable("value", 42);

        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.PrepareEnvironment(namedEnvironment));

        IRuntimeEnvironment resolved = runtime.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Reference,
            Name = "named"
        });
        Assert.IsTrue(resolved.TryResolveVariable("value", out int value));
        Assert.AreEqual(42, value);
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

    private sealed class ScopedExecutionProbe : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class SingletonExecutionProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed record ScopeProbeRecord(string Name, ScopedExecutionProbe ScopedProbe, SingletonExecutionProbe SingletonProbe, bool WasDisposedDuringExecution);

    private sealed class ScopeProbeRecorder
    {
        private readonly List<ScopeProbeRecord> _records = [];

        public IReadOnlyList<ScopeProbeRecord> Records => _records;

        public bool AllScopesAliveDuringExecution => _records.All(static record => !record.WasDisposedDuringExecution);

        public void Record(string name, ScopedExecutionProbe scopedProbe, SingletonExecutionProbe singletonProbe)
        {
            lock (_records)
            {
                _records.Add(new ScopeProbeRecord(name, scopedProbe, singletonProbe, scopedProbe.IsDisposed));
            }
        }

        public ScopeProbeRecord GetRequired(string name) => _records.Single(record => record.Name == name);
    }

    private sealed class ScopeProbeWorkerFactory(ScopeProbeRecorder recorder) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            ScopedExecutionProbe scopedProbe = serviceProvider.GetRequiredService<ScopedExecutionProbe>();
            SingletonExecutionProbe singletonProbe = serviceProvider.GetRequiredService<SingletonExecutionProbe>();
            return new ScopeProbeWorker((ProbeModule)moduleReference.Definition, scopedProbe, singletonProbe, recorder);
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            throw new NotSupportedException();

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule => throw new NotSupportedException();
    }

    private sealed class ScopeProbeWorker(
        ProbeModule module,
        ScopedExecutionProbe scopedProbe,
        SingletonExecutionProbe singletonProbe,
        ScopeProbeRecorder recorder) : IModuleWorker
    {
        public string ModuleId => ProbeModule.ModuleId;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            string name = module.Name ?? throw new InvalidOperationException("Scope probe modules require a name.");
            recorder.Record(name, scopedProbe, singletonProbe);
            if (name == "root")
            {
                ModuleReference child = new(new ProbeModule { Name = "child" }, ProbeModule.ModuleId);
                ModuleReference sibling = new(new ProbeModule { Name = "sibling" }, ProbeModule.ModuleId);
                await runtime.ExecuteAsync(child, runtime.Environment, cancellationToken);
                await runtime.ExecuteAsync(sibling, runtime.Environment, cancellationToken);
            }
            else if (name == "child")
            {
                ModuleReference grandchild = new(new ProbeModule { Name = "grandchild" }, ProbeModule.ModuleId);
                await runtime.ExecuteAsync(grandchild, runtime.Environment, cancellationToken);
            }
            recorder.Record($"{name}:after", scopedProbe, singletonProbe);
            return new ProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateArtifactCollection());
        }
    }

    private sealed class DelegatingServiceProvider(IServiceProvider inner) : IServiceProvider
    {
        public object? GetService(Type serviceType) => inner.GetService(serviceType);
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
