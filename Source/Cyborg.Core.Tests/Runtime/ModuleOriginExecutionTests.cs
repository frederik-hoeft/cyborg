using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Cyborg.Core.Tests.Runtime;

[TestClass]
public sealed class ModuleOriginExecutionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_InheritParent_PreservesDefinitionOriginAcrossLookupAsync()
    {
        ModuleOrigin callerOrigin = Origin("caller", "caller.jconf");
        ModuleOrigin childOrigin = Origin("child", "child.jconf");
        OriginLog log = new();
        using RuntimeHandle handle = new(log);
        RootModuleRuntime runtime = handle.Runtime;
        ModuleReference child = new(new ProbeModule { Name = "child" }, ProbeModule.ModuleId, childOrigin);
        ModuleReference root = new(new ProbeModule { Name = "root", Child = child, ChildScope = EnvironmentScope.InheritParent }, ProbeModule.ModuleId, callerOrigin);

        IModuleExecutionResult result = await runtime.ExecuteAsync(root, runtime.Environment, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Observation rootObservation = log.GetRequired("root");
        Observation childObservation = log.GetRequired("child");
        Assert.AreEqual(callerOrigin.File, rootObservation.File);
        Assert.AreEqual(callerOrigin.Directory, rootObservation.Directory);
        Assert.AreEqual($"{callerOrigin.File}|{callerOrigin.Directory}", rootObservation.Direct);
        Assert.AreEqual(childOrigin.File, childObservation.File);
        Assert.AreEqual(childOrigin.Directory, childObservation.Directory);
        Assert.AreEqual($"{childOrigin.File}|{childOrigin.Directory}", childObservation.Direct);
        Assert.AreEqual($"{callerOrigin.File}|{childOrigin.File}", childObservation.InheritedTemplate);
        Assert.AreEqual(callerOrigin.File, rootObservation.FileAfterChild);
        Assert.IsFalse(runtime.Environment.TryResolveVariable("@.origin.file", out string? _));
        Assert.IsFalse(runtime.Environment.Any(static pair => pair.Key.StartsWith("@.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ParentScope_DoesNotPublishOriginOntoCallerAsync()
    {
        ModuleOrigin callerOrigin = Origin("caller", "caller.jconf");
        ModuleOrigin childOrigin = Origin("other", "other.jconf");
        OriginLog log = new();
        using RuntimeHandle handle = new(log);
        RootModuleRuntime runtime = handle.Runtime;
        ModuleReference child = new(new ProbeModule { Name = "child", WriteMarker = true }, ProbeModule.ModuleId, childOrigin);
        ModuleReference root = new(new ProbeModule { Name = "root", Child = child, ChildScope = EnvironmentScope.Parent }, ProbeModule.ModuleId, callerOrigin);

        IModuleExecutionResult result = await runtime.ExecuteAsync(root, runtime.Environment, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreEqual(callerOrigin.File, log.GetRequired("root").FileAfterChild);
        Assert.AreEqual(childOrigin.File, log.GetRequired("child").File);
        Assert.IsTrue(runtime.Environment.TryResolveVariable("child_marker", out string? marker));
        Assert.AreEqual("set", marker);
        Assert.IsFalse(runtime.Environment.TryResolveVariable("@.origin.file", out string? _));
        Assert.IsFalse(runtime.Environment.TryResolveVariable("@.origin.directory", out string? _));
        Assert.IsFalse(runtime.Environment.Any(static pair => pair.Key.StartsWith("@.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteConcurrentlyAsync_SiblingOrigins_DoNotBleedIntoCallerAsync()
    {
        ModuleOrigin leftOrigin = Origin("left", "left.jconf");
        ModuleOrigin rightOrigin = Origin("right", "right.jconf");
        OriginLog log = new();
        using RuntimeHandle handle = new(log);
        RootModuleRuntime runtime = handle.Runtime;
        ModuleContext left = new(
            new ModuleReference(new ProbeModule { Name = "left", WriteMarker = true }, ProbeModule.ModuleId, leftOrigin),
            new ModuleEnvironment { Scope = EnvironmentScope.Parent },
            Configuration: null,
            ModuleRequirements.Default);
        ModuleContext right = new(
            new ModuleReference(new ProbeModule { Name = "right", WriteMarker = true }, ProbeModule.ModuleId, rightOrigin),
            new ModuleEnvironment { Scope = EnvironmentScope.Parent },
            Configuration: null,
            ModuleRequirements.Default);

        IReadOnlyList<IModuleExecutionResult> results = await runtime.ExecuteConcurrentlyAsync([left, right], TestContext.CancellationToken);

        Assert.IsTrue(results.All(static result => result.Status == ModuleExitStatus.Success));
        Assert.AreEqual(leftOrigin.File, log.GetRequired("left").File);
        Assert.AreEqual(rightOrigin.File, log.GetRequired("right").File);
        Assert.AreEqual(leftOrigin.Directory, log.GetRequired("left").Directory);
        Assert.AreEqual(rightOrigin.Directory, log.GetRequired("right").Directory);
        Assert.IsTrue(runtime.Environment.TryResolveVariable("left_marker", out string? _));
        Assert.IsTrue(runtime.Environment.TryResolveVariable("right_marker", out string? _));
        Assert.IsFalse(runtime.Environment.TryResolveVariable("@.origin.file", out string? _));
        Assert.IsFalse(runtime.Environment.Any(static pair => pair.Key.StartsWith("@.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutOrigin_LeavesContextualVariablesUndefinedAsync()
    {
        OriginLog log = new();
        using RuntimeHandle handle = new(log);
        RootModuleRuntime runtime = handle.Runtime;
        ModuleReference reference = new(new ProbeModule { Name = "plain" }, ProbeModule.ModuleId);

        IModuleExecutionResult result = await runtime.ExecuteAsync(reference, runtime.Environment, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Observation observation = log.GetRequired("plain");
        Assert.IsFalse(observation.HasFile);
        Assert.IsFalse(observation.HasDirectory);
        Assert.AreEqual("${@.origin.file}|${@@.origin.directory}", observation.Direct);
        Assert.IsFalse(runtime.Environment.TryResolveVariable("@.origin.file", out string? _));
    }

    [TestMethod]
    public async Task ExecuteAsync_IndirectReference_KeepsItsOwnOriginAsync()
    {
        ModuleOrigin callerOrigin = Origin("caller", "caller.jconf");
        ModuleOrigin loadedOrigin = Origin("templates", "backup.jconf");
        OriginLog log = new();
        using RuntimeHandle handle = new(log);
        RootModuleRuntime runtime = handle.Runtime;
        ModuleReference loaded = new(new ProbeModule { Name = "loaded" }, ProbeModule.ModuleId, loadedOrigin);
        ModuleReference root = new(new ProbeModule { Name = "root", Indirect = loaded }, ProbeModule.ModuleId, callerOrigin);

        IModuleExecutionResult result = await runtime.ExecuteAsync(root, runtime.Environment, TestContext.CancellationToken);

        Assert.AreEqual(ModuleExitStatus.Success, result.Status);
        Assert.AreEqual(callerOrigin.File, log.GetRequired("root").File);
        Assert.AreEqual(loadedOrigin.File, log.GetRequired("loaded").File);
        Assert.AreEqual(loadedOrigin.Directory, log.GetRequired("loaded").Directory);
        Assert.AreEqual(loadedOrigin, log.GetRequired("root").IndirectOrigin);
    }

    private static ModuleOrigin Origin(string directory, string file) =>
        ModuleOrigin.FromFile(Path.Combine(directory, file));

    private sealed class RuntimeHandle : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ServiceProvider _services;

        public RuntimeHandle(OriginLog log)
        {
            _loggerFactory = LoggerFactory.Create(static _ => { });
            _services = new ServiceCollection()
                .AddSingleton<IModuleWorkerFactory>(new OriginProbeFactory(log))
                .BuildServiceProvider();
            GlobalRuntimeEnvironment globalEnvironment = new(JsonNamingPolicy.SnakeCaseLower);
            Runtime = new RootModuleRuntime(globalEnvironment, _loggerFactory, _services);
        }

        public RootModuleRuntime Runtime { get; }

        public void Dispose()
        {
            _services.Dispose();
            _loggerFactory.Dispose();
        }
    }

    private sealed record ProbeModule : ModuleBase, IModule
    {
        public static string ModuleId => "cyborg.tests.origin.v1";

        public ModuleReference? Child { get; init; }

        public EnvironmentScope ChildScope { get; init; } = EnvironmentScope.InheritParent;

        public ModuleReference? Indirect { get; init; }

        public bool WriteMarker { get; init; }
    }

    private sealed record Observation(
        string Name,
        bool HasFile,
        string? File,
        bool HasDirectory,
        string? Directory,
        string Direct,
        string? InheritedTemplate,
        string? FileAfterChild,
        ModuleOrigin? IndirectOrigin);

    private sealed class OriginLog
    {
        private readonly List<Observation> _observations = [];

        public void Add(Observation observation)
        {
            lock (_observations)
            {
                _observations.Add(observation);
            }
        }

        public Observation GetRequired(string name) => _observations.Single(observation => observation.Name == name);
    }

    private sealed class OriginProbeFactory(OriginLog log) : IModuleWorkerFactory
    {
        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider) =>
            new OriginProbeWorker((ProbeModule)moduleReference.Definition, log);

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider) where TModule : class, IModule =>
            throw new NotSupportedException();

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            throw new NotSupportedException();
    }

    private sealed class OriginProbeWorker(ProbeModule module, OriginLog log) : IModuleWorker
    {
        public string ModuleId => ProbeModule.ModuleId;

        public IModule Module => module;

        async Task<IModuleExecutionResult> IModuleWorker.ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
        {
            bool hasFile = runtime.Environment.TryResolveVariable("@.origin.file", out string? file);
            bool hasDirectory = runtime.Environment.TryResolveVariable("@.origin.directory", out string? directory);
            string direct = runtime.Environment.Interpolate("${@.origin.file}|${@@.origin.directory}").Value;
            string? inherited = null;
            if (runtime.Environment.TryResolveVariable("from_parent", out string? inheritedValue))
            {
                inherited = inheritedValue;
            }
            if (hasFile)
            {
                string capturedFileName = $"{module.Name}_captured_file";
                string capturedDirectoryName = $"{module.Name}_captured_directory";
                runtime.Environment.SetVariable(capturedFileName, "*{@.origin.file}");
                runtime.Environment.SetVariable(capturedDirectoryName, "*{@@.origin.directory}");
                Assert.IsTrue(runtime.Environment.TryResolveVariable(capturedFileName, out string? capturedFile));
                Assert.IsTrue(runtime.Environment.TryResolveVariable(capturedDirectoryName, out string? capturedDirectory));
                Assert.AreEqual(file, capturedFile);
                Assert.AreEqual(directory, capturedDirectory);
                string indirectFileName = $"{module.Name}_indirect_file";
                runtime.Environment.SetVariable(indirectFileName, "&{@.origin.file}");
                Assert.IsTrue(runtime.Environment.TryResolveVariable(indirectFileName, out string? indirectFile));
                Assert.AreEqual(file, indirectFile);
            }
            ModuleOrigin? indirectOrigin = null;
            if (module.Indirect is { } indirect)
            {
                runtime.Environment.SetVariable("pending", indirect);
                runtime.Environment.SetVariable("pending_alias", "&{pending}");
                Assert.IsTrue(runtime.Environment.TryResolveVariable("pending_alias", out ModuleReference? resolved));
                indirectOrigin = resolved.Origin;
                await runtime.ExecuteAsync(resolved, runtime.Environment, cancellationToken);
            }
            if (module.Child is { } child)
            {
                runtime.Environment.SetVariable("from_parent", "${@.origin.file}|${@@.origin.file}");
                ModuleContext childContext = new(
                    child,
                    new ModuleEnvironment { Scope = module.ChildScope },
                    Configuration: null,
                    ModuleRequirements.Default);
                await runtime.ExecuteAsync(childContext, cancellationToken);
            }
            if (module.WriteMarker)
            {
                runtime.Environment.SetVariable($"{module.Name}_marker", "set");
            }
            bool stillHasFile = runtime.Environment.TryResolveVariable("@.origin.file", out string? fileAfterChild);
            log.Add(new Observation(
                module.Name ?? string.Empty,
                hasFile,
                file,
                hasDirectory,
                directory,
                direct,
                inherited,
                stillHasFile ? fileAfterChild : null,
                indirectOrigin));
            return new ProbeExecutionResult(module, ModuleExitStatus.Success, runtime.Environment.CreateTestArtifactCollection());
        }
    }

    private sealed record ProbeExecutionResult(IModule Module, ModuleExitStatus Status, IVariableResolverScope Artifacts) : IModuleExecutionResult;
}
