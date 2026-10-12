using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Modules.Dynamic;
using Cyborg.Modules.External;
using Cyborg.Modules.Sequence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Modules.Tests.Runtime;

[TestClass]
public sealed class ModuleOriginTests : ModuleTestBase
{
    [TestMethod]
    public Task LoadModuleAsync_StampsEveryReferenceWithTheDefiningFileAsync() => TestWithDIAsync(async services =>
    {
        using TempWorkflow workflow = await TempWorkflow.CreateAsync(TestContext.CancellationToken);
        IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();

        ModuleConfigurationLoadResult loaded = await loader.LoadModuleAsync(workflow.CallerPath, TestContext.CancellationToken);

        ModuleOrigin origin = loaded.ModuleContext.Module.Origin ?? throw new AssertFailedException("Caller module has no origin.");
        MSAssert.AreEqual(Path.GetFullPath(workflow.CallerPath), origin.File);
        MSAssert.AreEqual(Path.GetDirectoryName(Path.GetFullPath(workflow.CallerPath)), origin.Directory);
        SequenceModule sequence = (SequenceModule)loaded.ModuleContext.Module.Definition;
        foreach (ModuleContext step in sequence.Steps)
        {
            MSAssert.AreEqual(origin, step.Module.Origin);
        }
    });

    [TestMethod]
    public Task LoadModuleAsync_ParallelLoads_KeepIndependentOriginsAsync() => TestWithDIAsync(async services =>
    {
        using TempWorkflow workflow = await TempWorkflow.CreateAsync(TestContext.CancellationToken);
        IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();

        Task<ModuleConfigurationLoadResult> callerLoad = loader.LoadModuleAsync(workflow.CallerPath, TestContext.CancellationToken);
        Task<ModuleConfigurationLoadResult> childLoad = loader.LoadModuleAsync(workflow.ChildPath, TestContext.CancellationToken);
        ModuleConfigurationLoadResult callerConfiguration = await callerLoad;
        ModuleConfigurationLoadResult childConfiguration = await childLoad;

        MSAssert.AreEqual(Path.GetFullPath(workflow.CallerPath), callerConfiguration.ModuleContext.Module.Origin?.File);
        MSAssert.AreEqual(Path.GetFullPath(workflow.ChildPath), childConfiguration.ModuleContext.Module.Origin?.File);
        MSAssert.AreNotEqual(callerConfiguration.ModuleContext.Module.Origin, childConfiguration.ModuleContext.Module.Origin);
    });

    [TestMethod]
    public Task ExecuteAsync_FileAndDynamicReference_UseDefinitionOriginRatherThanCallSiteAsync() => TestWithDIAsync(async services =>
    {
        using TempWorkflow workflow = await TempWorkflow.CreateAsync(TestContext.CancellationToken);
        RecordingChildProcessDispatcher dispatcher = services.GetRequiredService<RecordingChildProcessDispatcher>();
        IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult caller = await loader.LoadModuleAsync(workflow.CallerPath, TestContext.CancellationToken);
        ModuleConfigurationLoadResult child = await loader.LoadModuleAsync(workflow.ChildPath, TestContext.CancellationToken);
        string callerFile = Path.GetFullPath(workflow.CallerPath);
        string childFile = Path.GetFullPath(workflow.ChildPath);
        DynamicModule dynamicModule = new(child.ModuleContext, []) { Name = "dynamic_caller" };
        ModuleReference dynamicReference = new(dynamicModule, DynamicModule.ModuleId, ModuleOrigin.FromFile(workflow.CallerPath));

        IModuleExecutionResult callerResult = await runtime.ExecuteAsync(caller, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, callerResult.Status);
        ChildProcessInvocation inline = dispatcher.Invocations.Single(invocation => invocation.ArgumentList.Count > 0 && invocation.ArgumentList[0].Value == "inline");
        MSAssert.AreEqual(callerFile, Argument(inline, 1));
        MSAssert.AreEqual(Path.GetDirectoryName(callerFile), Argument(inline, 2));
        MSAssert.AreEqual(callerFile, Argument(inline, 3));
        ChildProcessInvocation loaded = dispatcher.Invocations.Single(invocation => invocation.ArgumentList.Count > 0 && invocation.ArgumentList[0].Value == "loaded");
        MSAssert.AreEqual(childFile, Argument(loaded, 1));
        MSAssert.AreEqual(Path.GetDirectoryName(childFile), Argument(loaded, 2));
        MSAssert.AreEqual(childFile, Argument(loaded, 3));
        dispatcher.Clear();

        IModuleExecutionResult dynamicResult = await runtime.ExecuteAsync(dynamicReference, cancellationToken: TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, dynamicResult.Status);
        ChildProcessInvocation dynamicLoaded = dispatcher.Invocations.Single();
        MSAssert.AreEqual("loaded", Argument(dynamicLoaded, 0));
        MSAssert.AreEqual(childFile, Argument(dynamicLoaded, 1));
        MSAssert.AreEqual(Path.GetDirectoryName(childFile), Argument(dynamicLoaded, 2));
        MSAssert.AreEqual(childFile, Argument(dynamicLoaded, 3));
        MSAssert.IsFalse(runtime.Environment.TryResolveVariable("@.origin.file", out string? _));
        MSAssert.IsFalse(runtime.GlobalEnvironment.Any(static pair => pair.Key.StartsWith("@.", StringComparison.Ordinal)));
    }, configureServices: services =>
    {
        services.RemoveAll<IChildProcessDispatcher>();
        services.AddSingleton<RecordingChildProcessDispatcher>();
        services.AddSingleton<IChildProcessDispatcher>(static provider => provider.GetRequiredService<RecordingChildProcessDispatcher>());
    });

    [TestMethod]
    public Task ExecuteAsync_ExternalPath_ResolvesRelativeToTheDefiningDirectoryAsync() => TestWithDIAsync(async services =>
    {
        using TempWorkflow workflow = await TempWorkflow.CreateAsync(TestContext.CancellationToken);
        RecordingChildProcessDispatcher dispatcher = services.GetRequiredService<RecordingChildProcessDispatcher>();
        IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult externalCaller = await loader.LoadModuleAsync(workflow.ExternalCallerPath, TestContext.CancellationToken);

        IModuleExecutionResult result = await runtime.ExecuteAsync(externalCaller, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        ExternalModule external = (ExternalModule)externalCaller.ModuleContext.Module.Definition;
        MSAssert.AreEqual("${@.origin.directory}/child.jconf", external.Path);
        ChildProcessInvocation loaded = dispatcher.Invocations.Single();
        MSAssert.AreEqual(Path.GetFullPath(workflow.ChildPath), Argument(loaded, 1));
        MSAssert.AreEqual(Path.GetDirectoryName(Path.GetFullPath(workflow.ChildPath)), Argument(loaded, 2));
    }, configureServices: services =>
    {
        services.RemoveAll<IChildProcessDispatcher>();
        services.AddSingleton<RecordingChildProcessDispatcher>();
        services.AddSingleton<IChildProcessDispatcher>(static provider => provider.GetRequiredService<RecordingChildProcessDispatcher>());
    });

    private static string Argument(ChildProcessInvocation invocation, int index) => invocation.ArgumentList[index].Value;

    private sealed class RecordingChildProcessDispatcher : IChildProcessDispatcher
    {
        private readonly List<ChildProcessInvocation> _invocations = [];

        public IReadOnlyList<ChildProcessInvocation> Invocations
        {
            get
            {
                lock (_invocations)
                {
                    return [.. _invocations];
                }
            }
        }

        public void Clear()
        {
            lock (_invocations)
            {
                _invocations.Clear();
            }
        }

        public Task<ChildProcessResult> ExecuteAsync(ChildProcessInvocation invocation, CancellationToken cancellationToken)
        {
            lock (_invocations)
            {
                _invocations.Add(invocation);
            }
            return Task.FromResult(new ChildProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed class TempWorkflow : IDisposable
    {
        private readonly string _root;

        private TempWorkflow(string root)
        {
            _root = root;
            string callerDirectory = Path.Combine(root, "caller");
            string childDirectory = Path.Combine(root, "child");
            ChildDirectory = childDirectory;
            CallerPath = Path.Combine(callerDirectory, "caller.jconf");
            ChildPath = Path.Combine(childDirectory, "child.jconf");
            ExternalCallerPath = Path.Combine(childDirectory, "external-caller.jconf");
        }

        public string ChildDirectory { get; }

        public string CallerPath { get; }

        public string ChildPath { get; }

        public string ExternalCallerPath { get; }

        public static async Task<TempWorkflow> CreateAsync(CancellationToken cancellationToken)
        {
            string root = Path.Combine(Path.GetTempPath(), "cyborg-origin-" + Guid.NewGuid().ToString("N"));
            string executable = Path.Combine(root, "cyborg-origin-exec");
            TempWorkflow workflow = new(root);
            Directory.CreateDirectory(Path.GetDirectoryName(workflow.CallerPath)!);
            Directory.CreateDirectory(workflow.ChildDirectory);
            await File.WriteAllTextAsync(executable, string.Empty, cancellationToken);
            await File.WriteAllTextAsync(workflow.ChildPath, ChildJson(executable), cancellationToken);
            await File.WriteAllTextAsync(workflow.CallerPath, CallerJson(executable, workflow.ChildPath), cancellationToken);
            await File.WriteAllTextAsync(workflow.ExternalCallerPath, ExternalCallerJson(), cancellationToken);
            return workflow;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private static string ChildJson(string executable) => $$"""
            {
              "module": {
                "cyborg.modules.subprocess.v1": {
                  "name": "loaded_child",
                  "command": {
                    "executable": "{{JsonPath(executable)}}",
                    "arguments": ["loaded", "${@.origin.file}", "&{@.origin.directory}", "*{@@.origin.file}"]
                  }
                }
              }
            }
            """;

        private static string CallerJson(string executable, string childPath) => $$"""
            {
              "module": {
                "cyborg.modules.sequence.v1": {
                  "name": "caller",
                  "steps": [
                    {
                      "module": {
                        "cyborg.modules.subprocess.v1": {
                          "name": "inline_child",
                          "command": {
                            "executable": "{{JsonPath(executable)}}",
                            "arguments": ["inline", "${@.origin.file}", "&{@.origin.directory}", "*{@.origin.file}"]
                          }
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.external.v1": {
                          "name": "loads_child",
                          "path": "{{JsonPath(childPath)}}"
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

        private static string ExternalCallerJson() => $$"""
            {
              "module": {
                "cyborg.modules.external.v1": {
                  "name": "relative_load",
                  "path": "${@.origin.directory}/child.jconf"
                }
              }
            }
            """;

        private static string JsonPath(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
