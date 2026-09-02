using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Services.Debugging;
using Cyborg.Core.Runtime.Services.Debugging.Breakpoints;
using Cyborg.Core.Services.Default;
using Cyborg.Core.TestAdapter;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Cyborg.Modules.Tests.Runtime;

[TestClass]
public sealed class DebuggerProductionFlowTests : ModuleTestBase
{
    protected override void BuildConfiguration(IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        base.BuildConfiguration(configuration);

        IServiceSelectionKey<IDebugFrontend> frontendKey = configuration.ServiceProvider.GetRequiredService<IServiceSelectionKey<IDebugFrontend>>();
        configuration.AddDictionary(dict => dict.AddEntry(frontendKey.Key, "test"));
    }

    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jabServiceDiscovery);
        base.ConfigureServices(services, jabServiceDiscovery);

        services.AddSingleton<IDebugFrontend, ScriptedFrontend>();
    }

    [TestMethod]
    public Task Test_Sequence_StepAdvancesToNextChildAndContinueClearsItAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (context, _) =>
            ValueTask.FromResult(context.ValidationResult.Module.Name switch
            {
                "first" => DebugResumeAction.Step,
                _ => DebugResumeAction.Continue,
            }));
        services.GetRequiredService<IBreakpointRegistry>().Add("^first$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, SequenceWorkflowJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.AreSequenceEqual(["first", "second"], frontend.Names);
    });

    [TestMethod]
    public Task Test_Dynamic_StepFlowsIntoDynamicallyExecutedTargetAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (context, _) =>
            ValueTask.FromResult(context.ValidationResult.Module.Name switch
            {
                "dynamic" => DebugResumeAction.Step,
                _ => DebugResumeAction.Continue,
            }));
        services.GetRequiredService<IBreakpointRegistry>().Add("^dynamic$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, DynamicWorkflowJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.AreSequenceEqual(["dynamic", "dynamic-target"], frontend.Names);
        MSAssert.DoesNotContain("after", frontend.Names);
    });

    [TestMethod]
    public Task Test_Parallel_StepOnOneBranchDoesNotPauseSiblingBranchAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (context, _) =>
            ValueTask.FromResult(context.ValidationResult.Module.Name switch
            {
                "a1" => DebugResumeAction.Step,
                _ => DebugResumeAction.Continue,
            }));
        services.GetRequiredService<IBreakpointRegistry>().Add("^a1$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, ParallelBranchWorkflowJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.AreSequenceEqual(["a1", "a2"], frontend.Names);
        MSAssert.DoesNotContain("b1", frontend.Names);
        MSAssert.DoesNotContain("b2", frontend.Names);
        MSAssert.DoesNotContain("after", frontend.Names);
    });

    [TestMethod]
    public Task Test_Parallel_GlobalBreakpointDoesNotConsumeSiblingScopedStepAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (context, _) =>
            ValueTask.FromResult(context.ValidationResult.Module.Name switch
            {
                "a1" => DebugResumeAction.Step,
                _ => DebugResumeAction.Continue,
            }));
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        breakpoints.Add("^a1$");
        breakpoints.Add("^b1$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, ParallelBranchWorkflowJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.Contains("a1", frontend.Names);
        MSAssert.Contains("a2", frontend.Names);
        MSAssert.Contains("b1", frontend.Names);
        MSAssert.DoesNotContain("b2", frontend.Names);
        MSAssert.DoesNotContain("after", frontend.Names);
        MSAssert.HasCount(3, frontend.Names);
    });

    [TestMethod]
    public Task Test_Parallel_AllChildrenContinueClearsParentStepAtJoinAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (context, _) =>
            ValueTask.FromResult(context.ValidationResult.Module.Name switch
            {
                "fanout" => DebugResumeAction.Step,
                _ => DebugResumeAction.Continue,
            }));
        services.GetRequiredService<IBreakpointRegistry>().Add("^fanout$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, ShallowParallelWorkflowJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.Contains("fanout", frontend.Names);
        MSAssert.Contains("a", frontend.Names);
        MSAssert.Contains("b", frontend.Names);
        MSAssert.DoesNotContain("after", frontend.Names);
        MSAssert.HasCount(3, frontend.Names);
    });

    [TestMethod]
    public Task Test_Parallel_OneSteppingChildKeepsParentSteppingAfterJoinAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (context, _) =>
            ValueTask.FromResult(context.ValidationResult.Module.Name switch
            {
                "fanout" => DebugResumeAction.Step,
                "b" => DebugResumeAction.Step,
                _ => DebugResumeAction.Continue,
            }));
        services.GetRequiredService<IBreakpointRegistry>().Add("^fanout$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, ShallowParallelWorkflowJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.Contains("fanout", frontend.Names);
        MSAssert.Contains("a", frontend.Names);
        MSAssert.Contains("b", frontend.Names);
        MSAssert.Contains("after", frontend.Names);
        MSAssert.HasCount(4, frontend.Names);
    });

    [TestMethod]
    public Task Test_ConfigurationFailure_PreventsMainDebuggerBoundaryAndPrunesTopologyAsync() => TestWithDIAsync(async services =>
    {
        ScriptedFrontend frontend = UseFrontend(services, static (_, _) => ValueTask.FromResult(DebugResumeAction.Continue));
        services.GetRequiredService<IBreakpointRegistry>().Add("^main$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IDebugExecutionTopology topology = services.GetRequiredService<IDebugExecutionTopology>();
        string missingPath = Path.GetTempFileName();
        File.Delete(missingPath);
        string configurationJson = FailingConfigurationWorkflowJson.Replace(
            "__MISSING_PATH_JSON__",
            JsonSerializer.Serialize(missingPath),
            StringComparison.Ordinal);
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, configurationJson);

        IModuleExecutionResult result = await runtime.ExecuteAsync(configuration, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
        MSAssert.HasCount(0, frontend.Names);
        MSAssert.HasCount(0, topology.CaptureTree().Roots);
    });

    [TestMethod]
    public Task Test_Parallel_DetachSuppressesAlreadyQueuedSiblingPauseAsync() => TestWithDIAsync(async services =>
    {
        TaskCompletionSource firstPauseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedFrontend frontend = UseFrontend(services, async (_, index) =>
        {
            if (index == 0)
            {
                firstPauseEntered.TrySetResult();
                await releaseFirstPause.Task.ConfigureAwait(false);
                return DebugResumeAction.Detach;
            }
            return DebugResumeAction.Continue;
        });
        IBreakpointRegistry breakpoints = services.GetRequiredService<IBreakpointRegistry>();
        breakpoints.Add("^(a|b)$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IDebugExecutionTopology topology = services.GetRequiredService<IDebugExecutionTopology>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, ShallowParallelOnlyJson);

        Task<IModuleExecutionResult> execution = runtime.ExecuteAsync(configuration, TestContext.CancellationToken);
        await firstPauseEntered.Task.WaitAsync(TestContext.CancellationToken);
        await WaitForQueuedPauseAsync(topology, TestContext.CancellationToken);
        releaseFirstPause.TrySetResult();
        IModuleExecutionResult result = await execution;

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.HasCount(1, frontend.Names);
        MSAssert.AreEqual(0, breakpoints.Count);
        MSAssert.HasCount(0, topology.CaptureTree().Roots);
    });

    [TestMethod]
    public Task Test_Parallel_CancellationRemovesQueuedPauseAndClosesTopologyAsync() => TestWithDIAsync(async services =>
    {
        TaskCompletionSource firstPauseEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScriptedFrontend frontend = UseFrontend(services, async (_, index) =>
        {
            if (index == 0)
            {
                firstPauseEntered.TrySetResult();
                await releaseFirstPause.Task.ConfigureAwait(false);
            }
            return DebugResumeAction.Continue;
        });
        services.GetRequiredService<IBreakpointRegistry>().Add("^(a|b)$");
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        IDebugExecutionTopology topology = services.GetRequiredService<IDebugExecutionTopology>();
        ModuleConfigurationLoadResult configuration = await LoadContextAsync(services, ShallowParallelOnlyJson);
        using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);

        Task<IModuleExecutionResult> execution = runtime.ExecuteAsync(configuration, cancellationSource.Token);
        await firstPauseEntered.Task.WaitAsync(TestContext.CancellationToken);
        await WaitForQueuedPauseAsync(topology, TestContext.CancellationToken);
        await cancellationSource.CancelAsync();
        releaseFirstPause.TrySetResult();
        IModuleExecutionResult result = await execution;

        MSAssert.AreEqual(ModuleExitStatus.Canceled, result.Status);
        MSAssert.HasCount(1, frontend.Names);
        MSAssert.HasCount(0, topology.CaptureTree().Roots);
    });

    private static ScriptedFrontend UseFrontend(
        IServiceProvider services,
        Func<IDebugPauseContext, int, ValueTask<DebugResumeAction>> script)
    {
        IDebugFrontend frontend = services.GetRequiredService<IDebugFrontend>();
        MSAssert.IsInstanceOfType<ScriptedFrontend>(frontend);
        ScriptedFrontend typed = (ScriptedFrontend)frontend;
        typed.Script = script;
        return typed;
    }

    private async Task<ModuleConfigurationLoadResult> LoadContextAsync(IServiceProvider services, string json)
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json, TestContext.CancellationToken);
            IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
            return await loader.LoadModuleAsync(path, TestContext.CancellationToken);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task WaitForQueuedPauseAsync(IDebugExecutionTopology topology, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            IReadOnlyList<IExecutionTreeNode> nodes = Flatten(topology.CaptureTree().Roots);
            if (nodes.Any(static node => node.State is ExecutionTreeNodeState.Current)
                && nodes.Any(static node => node.State is ExecutionTreeNodeState.Paused))
            {
                return;
            }
            await Task.Delay(10, cancellationToken);
        }
        MSAssert.Fail("Timed out waiting for one active and one queued debugger pause.");
    }

    private static IReadOnlyList<IExecutionTreeNode> Flatten(IReadOnlyList<IExecutionTreeNode> roots)
    {
        List<IExecutionTreeNode> nodes = [];
        foreach (IExecutionTreeNode root in roots)
        {
            AddRecursive(root, nodes);
        }
        return nodes;
    }

    private static void AddRecursive(IExecutionTreeNode node, List<IExecutionTreeNode> nodes)
    {
        nodes.Add(node);
        foreach (IExecutionTreeNode child in node.Children)
        {
            AddRecursive(child, nodes);
        }
    }

    private const string SequenceWorkflowJson =
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.sequence.v1": {
              "name": "root",
              "steps": [
                { "module": { "cyborg.modules.empty.v1": { "name": "first" } } },
                { "module": { "cyborg.modules.empty.v1": { "name": "second" } } },
                { "module": { "cyborg.modules.empty.v1": { "name": "third" } } }
              ]
            }
          }
        }
        """;

    private const string DynamicWorkflowJson =
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.sequence.v1": {
              "name": "root",
              "steps": [
                {
                  "module": {
                    "cyborg.modules.dynamic.v1": {
                      "name": "dynamic",
                      "target": {
                        "module": {
                          "cyborg.modules.empty.v1": {
                            "name": "dynamic-target"
                          }
                        }
                      }
                    }
                  }
                },
                { "module": { "cyborg.modules.empty.v1": { "name": "after" } } }
              ]
            }
          }
        }
        """;

    private const string ParallelBranchWorkflowJson =
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.sequence.v1": {
              "name": "root",
              "steps": [
                {
                  "module": {
                    "cyborg.modules.parallel.v1": {
                      "name": "fanout",
                      "branches": [
                        {
                          "module": {
                            "cyborg.modules.sequence.v1": {
                              "name": "branch-a",
                              "steps": [
                                { "module": { "cyborg.modules.empty.v1": { "name": "a1" } } },
                                { "module": { "cyborg.modules.empty.v1": { "name": "a2" } } }
                              ]
                            }
                          }
                        },
                        {
                          "module": {
                            "cyborg.modules.sequence.v1": {
                              "name": "branch-b",
                              "steps": [
                                { "module": { "cyborg.modules.empty.v1": { "name": "b1" } } },
                                { "module": { "cyborg.modules.empty.v1": { "name": "b2" } } }
                              ]
                            }
                          }
                        }
                      ]
                    }
                  }
                },
                { "module": { "cyborg.modules.empty.v1": { "name": "after" } } }
              ]
            }
          }
        }
        """;

    private const string ShallowParallelWorkflowJson =
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.sequence.v1": {
              "name": "root",
              "steps": [
                {
                  "module": {
                    "cyborg.modules.parallel.v1": {
                      "name": "fanout",
                      "branches": [
                        { "module": { "cyborg.modules.empty.v1": { "name": "a" } } },
                        { "module": { "cyborg.modules.empty.v1": { "name": "b" } } }
                      ]
                    }
                  }
                },
                { "module": { "cyborg.modules.empty.v1": { "name": "after" } } }
              ]
            }
          }
        }
        """;

    private const string ShallowParallelOnlyJson =
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.parallel.v1": {
              "name": "fanout",
              "branches": [
                { "module": { "cyborg.modules.empty.v1": { "name": "a" } } },
                { "module": { "cyborg.modules.empty.v1": { "name": "b" } } }
              ]
            }
          }
        }
        """;

    private const string FailingConfigurationWorkflowJson =
        """
        {
          "environment": { "scope": "global" },
          "configuration": {
            "cyborg.modules.config.external.v1": {
              "path": __MISSING_PATH_JSON__
            }
          },
          "module": {
            "cyborg.modules.empty.v1": {
              "name": "main"
            }
          }
        }
        """;

    private sealed class ScriptedFrontend : IDebugFrontend
    {
        private readonly List<string> _names = [];

        public string Key => "test";

        public Func<IDebugPauseContext, int, ValueTask<DebugResumeAction>> Script { get; set; } =
            static (_, _) => ValueTask.FromResult(DebugResumeAction.Continue);

        public IReadOnlyList<string> Names
        {
            get
            {
                lock (_names)
                {
                    return [.. _names];
                }
            }
        }

        public ValueTask<DebugResumeAction> PauseAsync(IDebugPauseContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index;
            lock (_names)
            {
                index = _names.Count;
                _names.Add(context.ValidationResult.Module.Name ?? context.ModuleId);
            }
            return Script(context, index);
        }
    }
}
