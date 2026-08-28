using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.TestAdapter;
using Cyborg.Modules.Conditions.IsSet;
using Cyborg.Modules.Empty;
using Cyborg.Modules.Named;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cyborg.Modules.Tests.Runtime;

[TestClass]
public sealed class SequentialCompatibilityTests : ModuleTestBase
{
    [TestMethod]
    public Task ExecuteAsync_Sequence_JoinedChildStateIsVisibleToLaterStepsAndCallerAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.sequence.v1": {
                  "steps": [
                    {
                      "environment": { "scope": "parent" },
                      "module": {
                        "cyborg.modules.config.map.v1": {
                          "entries": [
                            { "key": "sequence_value", "string": "first" }
                          ]
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.condition.is_set.v1": {
                          "name": "sequence_check",
                          "variable": "sequence_value"
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("sequence_value", out string? value));
        MSAssert.AreEqual("first", value);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("sequence_check.result", out bool visibleToLaterStep));
        MSAssert.IsTrue(visibleToLaterStep);
    });

    [TestMethod]
    public Task ExecuteAsync_Foreach_IterationStateIsLocalAndArtifactsPublishToParentAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.foreach.v1": {
                  "collection": "items",
                  "item_variable": "item",
                  "body": {
                    "module": {
                      "cyborg.modules.condition.is_set.v1": {
                        "name": "iteration_check",
                        "variable": "item"
                      }
                    }
                  }
                }
              }
            }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("items", new object[] { "first", "second" });

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("item", out object? _));
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("iteration_check.result", out bool publishedResult));
        MSAssert.IsTrue(publishedResult);
        RecordingModuleWorkerFactory workerFactory = services.GetRequiredService<RecordingModuleWorkerFactory>();
        IModuleWorker[] iterationWorkers = [.. workerFactory.Workers.Where(static worker =>
            worker.ModuleId == IsSetModule.ModuleId
            && worker.Module.Name == "iteration_check")];
        MSAssert.HasCount(2, iterationWorkers);
        MSAssert.AreNotSame(iterationWorkers[0], iterationWorkers[1]);
    });

    [TestMethod]
    public Task ExecuteAsync_While_ConditionArtifactsComposeAcrossIterationsAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.while.v1": {
                  "condition": {
                    "cyborg.modules.condition.is_true.v1": {
                      "variable": "loop_enabled"
                    }
                  },
                  "body": {
                    "environment": { "scope": "parent" },
                    "module": {
                      "cyborg.modules.config.map.v1": {
                        "entries": [
                          { "key": "loop_enabled", "bool": false }
                        ]
                      }
                    }
                  }
                }
              }
            }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("loop_enabled", true);

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("loop_enabled", out bool loopEnabled));
        MSAssert.IsFalse(loopEnabled);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("cyborg.modules.while.v1.result", out bool finalCondition));
        MSAssert.IsFalse(finalCondition);
    });

    [TestMethod]
    public Task ExecuteAsync_If_ConditionArtifactPublishesToParentAndSelectedBranchRunsAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.if.v1": {
                  "condition": {
                    "cyborg.modules.condition.is_true.v1": {
                      "variable": "condition"
                    }
                  },
                  "then": {
                    "environment": { "scope": "parent" },
                    "module": {
                      "cyborg.modules.config.map.v1": {
                        "entries": [
                          { "key": "selected_branch", "string": "then" }
                        ]
                      }
                    }
                  },
                  "else": {
                    "environment": { "scope": "parent" },
                    "module": {
                      "cyborg.modules.config.map.v1": {
                        "entries": [
                          { "key": "selected_branch", "string": "else" }
                        ]
                      }
                    }
                  }
                }
              }
            }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("condition", true);

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("selected_branch", out string? branch));
        MSAssert.AreEqual("then", branch);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("cyborg.modules.if.v1.result", out bool condition));
        MSAssert.IsTrue(condition);
    });

    [TestMethod]
    public Task ExecuteAsync_ConfigurationStateIsVisibleDuringMainPreparationAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
            {
              "environment": { "scope": "global" },
              "configuration": {
                "cyborg.modules.config.collection.v1": {
                  "sources": [
                    {
                      "cyborg.modules.config.map.v1": {
                        "entries": [
                          { "key": "configured_variable", "string": "configured_flag" }
                        ]
                      }
                    },
                    {
                      "cyborg.modules.config.map.v1": {
                        "entries": [
                          { "key": "configured_flag", "bool": true }
                        ]
                      }
                    }
                  ]
                }
              },
              "module": {
                "cyborg.modules.condition.is_true.v1": {
                  "variable": "${configured_variable}"
                }
              }
            }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        MSAssert.IsTrue(result.Artifacts.TryResolveVariable("cyborg.modules.condition.is_true.v1.result", out bool condition));
        MSAssert.IsTrue(condition);
    });

    [TestMethod]
    public Task ExecuteAsync_NamedReference_ReactivatesWorkerAndUsesCurrentTransactionSnapshotAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.sequence.v1": {
                  "steps": [
                    {
                      "module": {
                        "cyborg.modules.empty.v1": {
                          "name": "first_target"
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.empty.v1": {
                          "name": "second_target"
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.named.ref.v1": {
                          "name": "shared_reference",
                          "target": "fallback"
                        }
                      }
                    },
                    {
                      "environment": { "scope": "parent" },
                      "module": {
                        "cyborg.modules.config.map.v1": {
                          "entries": [
                            { "key": "current_target", "string": "second_target" }
                          ]
                        }
                      }
                    },
                    {
                      "module": {
                        "cyborg.modules.named.ref.v1": {
                          "target": "shared_reference"
                        }
                      }
                    }
                  ]
                }
              }
            }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("current_target", "first_target");
        runtime.GlobalEnvironment.SetVariable("@shared_reference.target", "${current_target}");

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        RecordingModuleWorkerFactory workerFactory = services.GetRequiredService<RecordingModuleWorkerFactory>();
        IModuleWorker[] sharedReferenceWorkers = [.. workerFactory.Workers.Where(static worker =>
            worker.ModuleId == NamedModuleReferenceModule.ModuleId
            && worker.Module.Name == "shared_reference")];
        MSAssert.HasCount(2, sharedReferenceWorkers);
        MSAssert.AreNotSame(sharedReferenceWorkers[0], sharedReferenceWorkers[1]);
        MSAssert.AreEqual(2, workerFactory.Workers.Count(static worker =>
            worker.ModuleId == EmptyModule.ModuleId
            && worker.Module.Name == "first_target"));
        MSAssert.AreEqual(2, workerFactory.Workers.Count(static worker =>
            worker.ModuleId == EmptyModule.ModuleId
            && worker.Module.Name == "second_target"));
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("current_target", out string? currentTarget));
        MSAssert.AreEqual("second_target", currentTarget);
    });

    protected override void ConfigureServices(IServiceCollection services, IJabServiceDiscovery jabServiceDiscovery)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jabServiceDiscovery);
        base.ConfigureServices(services, jabServiceDiscovery);

        services.RemoveAll<IModuleWorkerFactory>();
        services.AddSingleton<RecordingModuleWorkerFactory>();
        services.AddSingleton<IModuleWorkerFactory>(static provider => provider.GetRequiredService<RecordingModuleWorkerFactory>());
    }

    private async Task<ModuleContext> LoadContextAsync(IServiceProvider services, string json)
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

    private sealed class RecordingModuleWorkerFactory(
        IModuleLoaderRegistry moduleLoaderRegistry,
        IEnumerable<IModuleLoader> moduleLoaders) : IModuleWorkerFactory
    {
        private readonly DefaultModuleWorkerFactory _inner = new(moduleLoaderRegistry, moduleLoaders);
        private readonly List<IModuleWorker> _workers = [];

        public IReadOnlyList<IModuleWorker> Workers
        {
            get
            {
                lock (_workers)
                {
                    return [.. _workers];
                }
            }
        }

        public IModuleWorker CreateWorker(ModuleReference moduleReference, IServiceProvider serviceProvider) =>
            Record(_inner.CreateWorker(moduleReference, serviceProvider));

        public IModuleWorker CreateWorker<TModule>(TModule module, string loader, IServiceProvider serviceProvider)
            where TModule : class, IModule =>
            Record(_inner.CreateWorker(module, loader, serviceProvider));

        public IModuleWorker CreateWorker<TModuleLoader, TModule>(TModule module, IServiceProvider serviceProvider)
            where TModuleLoader : IModuleLoader<TModule>
            where TModule : class, IModule =>
            Record(_inner.CreateWorker<TModuleLoader, TModule>(module, serviceProvider));

        private IModuleWorker Record(IModuleWorker worker)
        {
            lock (_workers)
            {
                _workers.Add(worker);
            }
            return worker;
        }
    }
}
