using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Modules.Parallel;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests.Parallel;

[TestClass]
public sealed class ParallelModuleTests : ModuleTestBase
{
    [TestMethod]
    public Task TestValidationAsync_EmptyBranches_IsInvalidAsync() =>
        TestValidationAsync<ParallelModule>(
            """
            {
              "cyborg.modules.parallel.v1": {
                "branches": []
              }
            }
            """,
            result => MSAssert.IsFalse(result.IsValid));

    [TestMethod]
    public Task TestExecutionAsync_BranchFailureAggregatesToFailedAsync() =>
        TestExecutionAsync(
            """
            {
              "cyborg.modules.parallel.v1": {
                "branches": [
                  {
                    "environment": { "scope": "current" },
                    "module": {
                      "cyborg.modules.empty.v1": {
                        "name": "successful_empty"
                      }
                    }
                  },
                  {
                    "environment": { "scope": "current" },
                    "module": {
                      "cyborg.modules.assert.v1": {
                        "name": "failing_assert",
                        "assertion": {
                          "cyborg.modules.condition.is_true.v1": {
                            "variable": "condition"
                          }
                        },
                        "message": "expected failure"
                      }
                    }
                  }
                ]
              }
            }
            """,
            result => MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status),
            environment => environment.SetVariable("condition", false));

    [TestMethod]
    public Task TestExecutionAsync_ConflictingSiblingWritesFailAtomicallyAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
                {
                  "environment": { "scope": "global" },
                  "module": {
                    "cyborg.modules.parallel.v1": {
                      "branches": [
                        {
                          "environment": { "scope": "current" },
                          "module": {
                            "cyborg.modules.config.map.v1": {
                              "name": "setter_first",
                              "entries": [
                                { "key": "shared", "string": "first" },
                                { "key": "first_only", "string": "first" }
                              ]
                            }
                          }
                        },
                        {
                          "environment": { "scope": "current" },
                          "module": {
                            "cyborg.modules.config.map.v1": {
                              "name": "setter_second",
                              "entries": [
                                { "key": "shared", "string": "second" },
                                { "key": "second_only", "string": "second" }
                              ]
                            }
                          }
                        }
                      ]
                    }
                  }
                }
            """);
        IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
        runtime.GlobalEnvironment.SetVariable("shared", "baseline");

        IModuleExecutionResult result = await runtime.ExecuteAsync(context, TestContext.CancellationToken);

        MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("shared", out string? shared));
        MSAssert.AreEqual("baseline", shared);
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("first_only", out string? _));
        MSAssert.IsFalse(runtime.GlobalEnvironment.TryResolveVariable("second_only", out string? _));
    });

    [TestMethod]
    public Task ExecuteAsync_NestedParallelPreservesDescendantWritesThroughOuterJoinAsync() => TestWithDIAsync(async services =>
    {
        ModuleContext context = await LoadContextAsync(services, """
                {
                  "environment": { "scope": "global" },
                  "module": {
                    "cyborg.modules.parallel.v1": {
                      "name": "outer_parallel",
                      "branches": [
                        {
                          "environment": { "scope": "current" },
                          "module": {
                            "cyborg.modules.parallel.v1": {
                              "name": "inner_parallel",
                              "branches": [
                                {
                                  "environment": { "scope": "current" },
                                  "module": {
                                    "cyborg.modules.config.map.v1": {
                                      "name": "inner_first",
                                      "entries": [
                                        { "key": "inner_first_value", "string": "first" }
                                      ]
                                    }
                                  }
                                },
                                {
                                  "environment": { "scope": "current" },
                                  "module": {
                                    "cyborg.modules.config.map.v1": {
                                      "name": "inner_second",
                                      "entries": [
                                        { "key": "inner_second_value", "string": "second" }
                                      ]
                                    }
                                  }
                                }
                              ]
                            }
                          }
                        },
                        {
                          "environment": { "scope": "current" },
                          "module": {
                            "cyborg.modules.config.map.v1": {
                              "name": "outer_sibling",
                              "entries": [
                                { "key": "outer_sibling_value", "string": "outer" }
                              ]
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
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("inner_first_value", out string? innerFirst));
        MSAssert.AreEqual("first", innerFirst);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("inner_second_value", out string? innerSecond));
        MSAssert.AreEqual("second", innerSecond);
        MSAssert.IsTrue(runtime.GlobalEnvironment.TryResolveVariable("outer_sibling_value", out string? outerSibling));
        MSAssert.AreEqual("outer", outerSibling);
    });

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

}
