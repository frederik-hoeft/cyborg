using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Services.Pipelines;
using Cyborg.TestModules.Activation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace Cyborg.Core.Tests.Configuration;

[TestClass]
public sealed class ModuleActivationTests
{
    [TestMethod]
    public void LoadModule_DoesNotResolveWorkerDependenciesUntilActivation()
    {
        ActivationProbeModuleLoader loader = new();
        using ServiceProvider loadingServices = new ServiceCollection()
            .AddSingleton(new ActivationProbeDependency("loading"))
            .BuildServiceProvider();
        TestJsonLoaderContext loadingContext = new(loadingServices);
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes("{}"));
        Assert.IsTrue(reader.Read());

        bool loaded = loader.TryLoadModule(ref reader, loadingContext, out IModule? definition);

        Assert.IsTrue(loaded);
        Assert.IsInstanceOfType<ActivationProbeModule>(definition);

        using ServiceProvider executionServices = CreateActivationServices("execution");
        IModuleLoader[] loaders = [loader];
        DefaultModuleWorkerFactory workerFactory = new(new DefaultModuleLoaderRegistry(loaders), loaders);
        ModuleReference moduleReference = new(definition, loader.ModuleId);

        ActivationProbeModuleWorker worker = (ActivationProbeModuleWorker)workerFactory.CreateWorker(moduleReference, executionServices);

        Assert.AreEqual("execution", worker.Dependency.Identity);
    }

    [TestMethod]
    public async Task CreateModule_ConcurrentActivationsOfSameReferenceReturnIndependentWorkersAsync()
    {
        ActivationProbeModuleLoader loader = new();
        IModuleLoader[] loaders = [loader];
        DefaultModuleWorkerFactory workerFactory = new(new DefaultModuleLoaderRegistry(loaders), loaders);
        ModuleReference moduleReference = new(new ActivationProbeModule(), loader.ModuleId);
        using ServiceProvider executionServices = CreateActivationServices("execution");

        Task<ActivationProbeModuleWorker>[] activations = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => (ActivationProbeModuleWorker)workerFactory.CreateWorker(moduleReference, executionServices)))
            .ToArray();
        ActivationProbeModuleWorker[] workers = await Task.WhenAll(activations);

        Assert.HasCount(16, workers.Distinct().ToArray());
        foreach (ActivationProbeModuleWorker worker in workers)
        {
            Assert.AreSame(moduleReference.Definition, ((IModuleWorker)worker).Module);
            Assert.AreEqual("execution", worker.Dependency.Identity);
        }
    }

    private static ServiceProvider CreateActivationServices(string dependencyIdentity)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTransient(_ => new ActivationProbeDependency(dependencyIdentity));
        services.AddSingleton<IServicePipeline<IModuleValidationHook>>(new ServicePipeline<IModuleValidationHook>([]));
        services.AddSingleton<IServicePipeline<IModulePreExecutionHook>>(new ServicePipeline<IModulePreExecutionHook>([]));
        return services.BuildServiceProvider();
    }

    private sealed class TestJsonLoaderContext(IServiceProvider serviceProvider) : IJsonLoaderContext
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public JsonSerializerOptions JsonSerializerOptions { get; } = new();
    }
}
