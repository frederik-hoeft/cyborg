using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class RootExecutionSessionTests : CyborgCoreTestBase
{
    [TestMethod]
    public Task ResolveRuntime_MultipleRoots_IsolateWorkflowStateAsync() => TestWithDIAsync(services =>
    {
        IModuleRuntime first = services.GetRequiredService<IModuleRuntime>();
        IModuleRuntime second = services.GetRequiredService<IModuleRuntime>();

        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.GlobalEnvironment, second.GlobalEnvironment);

        first.GlobalEnvironment.SetVariable("session", "first");
        Assert.IsTrue(first.GlobalEnvironment.TryResolveVariable("session", out string? firstValue));
        Assert.AreEqual("first", firstValue);
        Assert.IsFalse(second.GlobalEnvironment.TryResolveVariable("session", out string? _));

        IRuntimeEnvironment namedEnvironment = first.PrepareEnvironment(new ModuleEnvironment
        {
            Scope = EnvironmentScope.Isolated,
            Name = "named"
        });
        namedEnvironment.SetVariable("value", 1);

        IRuntimeEnvironment? secondNamedEnvironment = second.ResolveEnvironmentReference(
            new ModuleEnvironmentReference(EnvironmentScopeReference.Reference, "named"));
        Assert.IsNull(secondNamedEnvironment);
    });

    [TestMethod]
    public Task ExecuteAsync_MultipleRoots_ShareOrdinarySingletonServicesAsync() => TestWithDIAsync(
        async services =>
        {
            IModuleRuntime first = services.GetRequiredService<IModuleRuntime>();
            IModuleRuntime second = services.GetRequiredService<IModuleRuntime>();
            ModuleReference module = new(new SessionProbeModule(), SessionProbeModule.ModuleId);

            IModuleExecutionResult firstResult = await first.ExecuteAsync(module, cancellationToken: TestContext.CancellationToken);
            IModuleExecutionResult secondResult = await second.ExecuteAsync(module, cancellationToken: TestContext.CancellationToken);

            Assert.AreEqual(ModuleExitStatus.Success, firstResult.Status);
            Assert.AreEqual(ModuleExitStatus.Success, secondResult.Status);
            SessionProbeRecorder recorder = services.GetRequiredService<SessionProbeRecorder>();
            Assert.HasCount(2, recorder.SingletonProbes);
            Assert.AreSame(recorder.SingletonProbes[0], recorder.SingletonProbes[1]);
        },
        configureServices: services =>
        {
            services.AddSingleton<SessionProbeRecorder>();
            services.AddSingleton<SessionSingletonProbe>();
            services.AddSingleton<IModuleWorkerFactory, SessionProbeWorkerFactory>();
        });

    private sealed record SessionProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.execution-session-probe.v1";
    }

    private sealed class SessionProbeWorker(SessionProbeModule module, SessionSingletonProbe singletonProbe, SessionProbeRecorder recorder) : IModuleWorker
    {
        public string ModuleId => SessionProbeModule.ModuleId;

        public IModule Module => module;

        Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            recorder.Record(singletonProbe);
            return Task.FromResult<IModuleExecutionResult>(
                new SessionProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection()));
        }
    }

    private sealed class SessionProbeWorkerFactory(SessionProbeRecorder recorder) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider)
        {
            SessionProbeModule module = (SessionProbeModule)moduleReference.Definition;
            SessionSingletonProbe singletonProbe = serviceProvider.GetRequiredService<SessionSingletonProbe>();
            return new SessionProbeWorker(module, singletonProbe, recorder);
        }

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            CreateWorker(new ModuleReference(module, loader), serviceProvider);

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class SessionProbeRecorder
    {
        private readonly List<SessionSingletonProbe> _singletonProbes = [];

        public IReadOnlyList<SessionSingletonProbe> SingletonProbes => _singletonProbes;

        public void Record(SessionSingletonProbe singletonProbe)
        {
            lock (_singletonProbes)
            {
                _singletonProbes.Add(singletonProbe);
            }
        }
    }

    private sealed class SessionSingletonProbe;

    private sealed record SessionProbeExecutionResult(
        IModule Module,
        ModuleExitStatus Status,
        IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
