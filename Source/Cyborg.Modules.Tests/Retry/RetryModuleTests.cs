using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Runtime;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Hooks;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.Transactions;
using Cyborg.Core.TestAdapter;
using Cyborg.Modules.Assert;
using Cyborg.Modules.Retry;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Cyborg.Modules.Tests.Retry;

[TestClass]
public sealed class RetryModuleTests : ModuleTestBase
{
    [TestMethod]
    public Task TestValidationAsync_AttemptsBelowOne_IsInvalidAsync() =>
        TestValidationAsync<RetryModule>(
            """
            {
              "cyborg.modules.retry.v1": {
                "attempts": 0,
                "body": {
                  "module": { "cyborg.modules.empty.v1": {} }
                }
              }
            }
            """,
            result => MSAssert.IsFalse(result.IsValid));

    [TestMethod]
    public Task TestValidationAsync_MissingAttempts_IsInvalidAsync() =>
        TestValidationAsync<RetryModule>(
            """
            {
              "cyborg.modules.retry.v1": {
                "body": {
                  "module": { "cyborg.modules.empty.v1": {} }
                }
              }
            }
            """,
            result => MSAssert.IsFalse(result.IsValid));

    [TestMethod]
    public Task TestValidationAsync_AttemptsAboveLimit_IsInvalidAsync() =>
        TestValidationAsync<RetryModule>(
            $$"""
            {
              "cyborg.modules.retry.v1": {
                "attempts": {{RetryModule.MAX_ATTEMPTS + 1}},
                "body": {
                  "module": { "cyborg.modules.empty.v1": {} }
                }
              }
            }
            """,
            result => MSAssert.IsFalse(result.IsValid));

    [TestMethod]
    public Task TestValidationAsync_MaximumAttempts_IsValidAsync() =>
        TestValidationAsync<RetryModule>(
            $$"""
            {
              "cyborg.modules.retry.v1": {
                "attempts": {{RetryModule.MAX_ATTEMPTS}},
                "body": {
                  "module": { "cyborg.modules.empty.v1": {} }
                }
              }
            }
            """,
            result => MSAssert.IsTrue(result.IsValid));

    [TestMethod]
    public Task TestValidationAsync_UndefinedTransactionOnError_IsInvalidAsync() =>
        TestValidationAsync<RetryModule>(
            """
            {
              "cyborg.modules.retry.v1": {
                "attempts": 1,
                "transaction": { "on_error": 2147483647 },
                "body": {
                  "module": { "cyborg.modules.empty.v1": {} }
                }
              }
            }
            """,
            result => MSAssert.IsFalse(result.IsValid));

    [TestMethod]
    public Task TestExecutionAsync_SecondAttemptSucceedsWhenFailedAttemptCommittedAsync() =>
        TestModuleContextAsync(
            RetryOverMarker("""
                "transaction": { "on_error": "commit" },
                """),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
                MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("marker", out string? marker));
                MSAssert.AreEqual("set", marker);
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task TestExecutionAsync_RollbackIsolatesFailedAttemptsAsync() =>
        TestModuleContextAsync(
            RetryOverMarker("""
                "transaction": { "on_error": "rollback" },
                """),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("marker", out object? _));
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task TestExecutionAsync_RetryRollbackHidesCommittedAttemptFromCallerAsync() =>
        TestModuleContextAsync(
            SingleFailingWrite("commit", "rollback"),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("marker", out object? _));
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task TestExecutionAsync_RetryCommitPublishesCommittedAttemptAsync() =>
        TestModuleContextAsync(
            SingleFailingWrite("commit", "commit"),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("marker", out string? marker));
                MSAssert.AreEqual("set", marker);
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task TestExecutionAsync_ChildRollbackLeavesNothingForRetryCommitAsync() =>
        TestModuleContextAsync(
            SingleFailingWrite("rollback", "commit"),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("marker", out object? _));
                return Task.CompletedTask;
            });

    [TestMethod]
    public Task TestExecutionAsync_GlobalRollbackAppliesWhenModuleOmitsOnErrorAsync() =>
        TestModuleContextAsync(
            SingleFailingWrite(bodyOnError: null, retryOnError: null),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("marker", out object? _));
                return Task.CompletedTask;
            },
            buildConfiguration: configuration => configuration.AddDictionary(new Dictionary<string, object>
            {
                [ITransactionOptionsProvider.ON_ERROR_KEY] = TransactionOnError.Rollback,
            }));

    [TestMethod]
    public Task TestExecutionAsync_ModuleCommitOverridesGlobalRollbackAsync() =>
        TestModuleContextAsync(
            SingleFailingWrite("commit", "commit"),
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("marker", out string? marker));
                MSAssert.AreEqual("set", marker);
                return Task.CompletedTask;
            },
            buildConfiguration: configuration => configuration.AddDictionary(new Dictionary<string, object>
            {
                [ITransactionOptionsProvider.ON_ERROR_KEY] = TransactionOnError.Rollback,
            }));

    [TestMethod]
    public async Task TestExecutionAsync_CancellationAfterFailedAttemptStopsRetriesAsync()
    {
        using CancellationTokenSource cancellation = new();
        CancelAfterFailedAttemptHook hook = new(cancellation);
        await TestWithDIAsync(async services =>
        {
            IJsonLoaderContext loaderContext = services.GetRequiredService<IJsonLoaderContext>();
            ModuleReference retry = JsonSerializer.Deserialize<ModuleReference>(
                """
                {
                  "cyborg.modules.retry.v1": {
                    "attempts": 5,
                    "body": {
                      "module": {
                        "cyborg.modules.assert.v1": {
                          "assertion": {
                            "cyborg.modules.condition.is_true.v1": { "variable": "missing" }
                          },
                          "message": "fail attempt"
                        }
                      }
                    }
                  }
                }
                """,
                loaderContext.JsonSerializerOptions) ?? throw new InvalidOperationException("Unable to deserialize retry module.");

            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();
            IModuleExecutionResult result = await runtime.ExecuteAsync(retry, cancellationToken: cancellation.Token);

            MSAssert.AreEqual(ModuleExitStatus.Canceled, result.Status);
            MSAssert.AreEqual(1, hook.FailedAttempts);
        }, configureServices: services => services.AddSingleton<IModulePostExecutionHook>(hook));
    }

    [TestMethod]
    public Task TestExecutionAsync_ParallelRollbackBranchDoesNotPublishAsync() =>
        TestModuleContextAsync(
            """
            {
              "environment": { "scope": "global" },
              "module": {
                "cyborg.modules.parallel.v1": {
                  "branches": [
                    {
                      "environment": { "scope": "current" },
                      "module": {
                        "cyborg.modules.sequence.v1": {
                          "transaction": { "on_error": "rollback" },
                          "steps": [
                            {
                              "environment": { "scope": "parent" },
                              "module": {
                                "cyborg.modules.config.map.v1": {
                                  "entries": [ { "key": "rolled", "string": "hidden" } ]
                                }
                              }
                            },
                            {
                              "module": {
                                "cyborg.modules.assert.v1": {
                                  "assertion": {
                                    "cyborg.modules.condition.is_true.v1": { "variable": "missing" }
                                  },
                                  "message": "fail rolled branch"
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
                        "cyborg.modules.sequence.v1": {
                          "transaction": { "on_error": "commit" },
                          "steps": [
                            {
                              "environment": { "scope": "parent" },
                              "module": {
                                "cyborg.modules.config.map.v1": {
                                  "entries": [ { "key": "kept", "string": "visible" } ]
                                }
                              }
                            },
                            {
                              "module": {
                                "cyborg.modules.assert.v1": {
                                  "assertion": {
                                    "cyborg.modules.condition.is_true.v1": { "variable": "missing" }
                                  },
                                  "message": "fail kept branch"
                                }
                              }
                            }
                          ]
                        }
                      }
                    }
                  ]
                }
              }
            }
            """,
            (result, scope) =>
            {
                MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
                MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("rolled", out object? _));
                MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("kept", out string? kept));
                MSAssert.AreEqual("visible", kept);
                return Task.CompletedTask;
            });

    private static string RetryOverMarker(string bodyTransactionProperty) =>
        $$"""
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.retry.v1": {
              "attempts": 2,
              "body": {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.sequence.v1": {
                    {{bodyTransactionProperty}}
                    "steps": [
                      {
                        "environment": { "scope": "parent" },
                        "module": {
                          "cyborg.modules.if.v1": {
                            "condition": {
                              "cyborg.modules.condition.is_set.v1": { "variable": "marker" }
                            },
                            "then": {
                              "module": { "cyborg.modules.empty.v1": {} }
                            },
                            "else": {
                              "module": {
                                "cyborg.modules.sequence.v1": {
                                  "steps": [
                                    {
                                      "environment": { "scope": "parent" },
                                      "module": {
                                        "cyborg.modules.config.map.v1": {
                                          "entries": [ { "key": "marker", "string": "set" } ]
                                        }
                                      }
                                    },
                                    {
                                      "module": {
                                        "cyborg.modules.assert.v1": {
                                          "assertion": {
                                            "cyborg.modules.condition.is_true.v1": { "variable": "missing" }
                                          },
                                          "message": "fail attempt"
                                        }
                                      }
                                    }
                                  ]
                                }
                              }
                            }
                          }
                        }
                      }
                    ]
                  }
                }
              }
            }
          }
        }
        """;

    private static string SingleFailingWrite(string? bodyOnError, string? retryOnError)
    {
        string retryTransaction = retryOnError is null ? string.Empty : $"\"transaction\": {{ \"on_error\": \"{retryOnError}\" }},";
        string bodyTransaction = bodyOnError is null ? string.Empty : $"\"transaction\": {{ \"on_error\": \"{bodyOnError}\" }},";
        return $$"""
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.retry.v1": {
              "attempts": 1,
              {{retryTransaction}}
              "body": {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.sequence.v1": {
                    {{bodyTransaction}}
                    "steps": [
                      {
                        "environment": { "scope": "parent" },
                        "module": {
                          "cyborg.modules.config.map.v1": {
                            "entries": [ { "key": "marker", "string": "set" } ]
                          }
                        }
                      },
                      {
                        "module": {
                          "cyborg.modules.assert.v1": {
                            "assertion": {
                              "cyborg.modules.condition.is_true.v1": { "variable": "missing" }
                            },
                            "message": "fail once"
                          }
                        }
                      }
                    ]
                  }
                }
              }
            }
          }
        }
        """;
    }

    private sealed class CancelAfterFailedAttemptHook(CancellationTokenSource cancellation) : IModulePostExecutionHook
    {
        public int FailedAttempts { get; private set; }

        public int Priority => 0;

        public async ValueTask ExecuteAsync(IModulePostExecutionContext context, CancellationToken cancellationToken)
        {
            if (context.Result is { Module: AssertModule, Status: ModuleExitStatus.Failed })
            {
                ++FailedAttempts;
                await cancellation.CancelAsync();
            }
        }
    }
}
