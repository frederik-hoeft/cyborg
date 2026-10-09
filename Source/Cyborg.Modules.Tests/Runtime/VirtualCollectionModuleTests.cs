using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.TestAdapter;

namespace Cyborg.Modules.Tests.Runtime;

[TestClass]
public sealed class VirtualCollectionModuleTests : ModuleTestBase
{
    [TestMethod]
    public Task TestExecutionAsync_SequentialAppends_JoinInWriteOrderAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.sequence.v1": {
              "steps": [
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "entries": [
                        { "key": "items[]+", "string": "first" }
                      ]
                    }
                  }
                },
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "entries": [
                        { "key": "items[]+", "string": "second" },
                        { "key": "items[]+", "int": 3 }
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
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.AreSequenceEqual(new object?[] { "first", "second", 3 }, ReadSnapshot(scope.GlobalEnvironment, "items[]"));
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task TestExecutionAsync_ParallelAppends_MergeWithoutConflictsAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.parallel.v1": {
              "branches": [
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "first_writer",
                      "entries": [
                        { "key": "items[]+", "string": "a1" },
                        { "key": "items[]+", "string": "a2" }
                      ]
                    }
                  }
                },
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "second_writer",
                      "entries": [
                        { "key": "items[]+", "string": "b1" }
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
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            object?[] items = ReadSnapshot(scope.GlobalEnvironment, "items[]");
            MSAssert.HasCount(3, items);
            MSAssert.IsTrue(items.Contains("a1"));
            MSAssert.IsTrue(items.Contains("a2"));
            MSAssert.IsTrue(items.Contains("b1"));
            MSAssert.IsTrue(Array.IndexOf(items, "a1") < Array.IndexOf(items, "a2"));
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task TestExecutionAsync_ParallelEmptyDefinitions_MergeAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.parallel.v1": {
              "branches": [
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "first_empty_definition",
                      "entries": [
                        { "key": "items[]", "collection<string>": [] }
                      ]
                    }
                  }
                },
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "second_empty_definition",
                      "entries": [
                        { "key": "items[]", "collection<string>": [] }
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
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("items[]", out IEnumerable<object>? items));
            MSAssert.IsNotNull(items);
            MSAssert.IsEmpty(items);
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task TestExecutionAsync_ParallelEmptyDefinitions_ReplaceExistingCollectionWithoutConflictAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.parallel.v1": {
              "branches": [
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "first_empty_replacement",
                      "entries": [
                        { "key": "items[]", "collection<string>": [] }
                      ]
                    }
                  }
                },
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "second_empty_replacement",
                      "entries": [
                        { "key": "items[]", "collection<string>": [] }
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
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("items[]", out IEnumerable<object>? items));
            MSAssert.IsNotNull(items);
            MSAssert.IsEmpty(items);
            return Task.CompletedTask;
        },
        environment => environment.SetVariable("items[]+", "baseline"));

    [TestMethod]
    public Task TestExecutionAsync_ParallelDefinitions_ConflictAndPublishNothingAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.parallel.v1": {
              "branches": [
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "first_definition",
                      "entries": [
                        { "key": "items[]", "collection<string>": ["first"] }
                      ]
                    }
                  }
                },
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "name": "second_definition",
                      "entries": [
                        { "key": "items[]", "collection<string>": ["second"] }
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
            MSAssert.AreSequenceEqual(new object?[] { "baseline" }, ReadSnapshot(scope.GlobalEnvironment, "items[]"));
            return Task.CompletedTask;
        },
        environment => environment.SetVariable("items[]+", "baseline"));

    [TestMethod]
    public Task TestExecutionAsync_Rollback_WithholdsAppendsAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.sequence.v1": {
              "transaction": { "on_error": "rollback" },
              "steps": [
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.config.map.v1": {
                      "entries": [
                        { "key": "items[]+", "string": "rolled-back" }
                      ]
                    }
                  }
                },
                {
                  "environment": { "scope": "current" },
                  "module": {
                    "cyborg.modules.assert.v1": {
                      "assertion": {
                        "cyborg.modules.condition.is_true.v1": {
                          "variable": "missing"
                        }
                      },
                      "message": "fail"
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
            MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("items[]", out IEnumerable<object>? _));
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task TestExecutionAsync_ForeachSnapshot_DoesNotObserveElementsAppendedDuringIterationAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "items[]",
              "item_variable": "item",
              "body": {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.config.map.v1": {
                    "entries": [
                      { "key": "items[]+", "string": "extra" },
                      { "key": "seen[]+", "string": "x" }
                    ]
                  }
                }
              }
            }
          }
        }
        """,
        (result, scope) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.AreSequenceEqual(new object?[] { "a", "b", "extra", "extra" }, ReadSnapshot(scope.GlobalEnvironment, "items[]"));
            MSAssert.AreSequenceEqual(new object?[] { "x", "x" }, ReadSnapshot(scope.GlobalEnvironment, "seen[]"));
            return Task.CompletedTask;
        },
        environment =>
        {
            environment.SetVariable("items[]+", "a");
            environment.SetVariable("items[]+", "b");
        });

    [TestMethod]
    public Task TestExecutionAsync_ForeachLiveView_VisitsElementsAppendedDuringIterationAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "items[+]",
              "item_variable": "item",
              "body": {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.sequence.v1": {
                    "steps": [
                      {
                        "environment": { "scope": "current" },
                        "module": {
                          "cyborg.modules.if.v1": {
                            "invert_condition": true,
                            "condition": {
                              "cyborg.modules.condition.is_set.v1": { "variable": "expanded" }
                            },
                            "then": {
                              "environment": { "scope": "current" },
                              "module": {
                                "cyborg.modules.config.map.v1": {
                                  "entries": [
                                    { "key": "expanded", "bool": true },
                                    { "key": "items[]+", "string": "extra" }
                                  ]
                                }
                              }
                            }
                          }
                        }
                      },
                      {
                        "environment": { "scope": "current" },
                        "module": {
                          "cyborg.modules.config.map.v1": {
                            "entries": [
                              { "key": "seen[]+", "string": "x" }
                            ]
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
        """,
        (result, scope) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.AreSequenceEqual(new object?[] { "a", "b", "extra" }, ReadSnapshot(scope.GlobalEnvironment, "items[]"));
            MSAssert.AreSequenceEqual(new object?[] { "x", "x", "x" }, ReadSnapshot(scope.GlobalEnvironment, "seen[]"));
            return Task.CompletedTask;
        },
        environment =>
        {
            environment.SetVariable("items[]+", "a");
            environment.SetVariable("items[]+", "b");
        });

    [TestMethod]
    public Task TestExecutionAsync_Foreach_EmptyCollectionIsSkippedAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "present[]",
              "item_variable": "item",
              "body": {
                "module": { "cyborg.modules.empty.v1": {} }
              }
            }
          }
        }
        """,
        (result, scope) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Skipped, result.Status);
            MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("present[]", out IEnumerable<object>? present));
            MSAssert.IsNotNull(present);
            MSAssert.IsEmpty(present);
            return Task.CompletedTask;
        },
        environment => environment.SetVariable<object?>("present[]", null));

    [TestMethod]
    public Task TestExecutionAsync_Foreach_MissingCollectionFailsAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "missing[]",
              "item_variable": "item",
              "body": {
                "module": { "cyborg.modules.empty.v1": {} }
              }
            }
          }
        }
        """,
        (result, _) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Failed, result.Status);
            return Task.CompletedTask;
        });

    [TestMethod]
    public Task TestExecutionAsync_Foreach_ClrCollectionIsUnchangedAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "global" },
          "module": {
            "cyborg.modules.foreach.v1": {
              "collection": "items",
              "item_variable": "item",
              "body": {
                "environment": { "scope": "parent" },
                "module": {
                  "cyborg.modules.config.map.v1": {
                    "entries": [
                      { "key": "seen[]+", "string": "x" }
                    ]
                  }
                }
              }
            }
          }
        }
        """,
        (result, scope) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.AreSequenceEqual(new object?[] { "x", "x" }, ReadSnapshot(scope.GlobalEnvironment, "seen[]"));
            MSAssert.IsTrue(scope.GlobalEnvironment.TryResolveVariable("items", out object[]? items));
            MSAssert.IsNotNull(items);
            MSAssert.AreSequenceEqual(new object[] { "first", "second" }, items);
            return Task.CompletedTask;
        },
        environment => environment.SetVariable("items", new object[] { "first", "second" }));

    [TestMethod]
    public Task TestExecutionAsync_IsolatedAppend_IsNotVisibleToGlobalAsync() => TestModuleContextAsync(
        """
        {
          "environment": { "scope": "isolated" },
          "module": {
            "cyborg.modules.config.map.v1": {
              "entries": [
                { "key": "items[]+", "string": "hidden" }
              ]
            }
          }
        }
        """,
        (result, scope) =>
        {
            MSAssert.AreEqual(ModuleExitStatus.Success, result.Status);
            MSAssert.IsFalse(scope.GlobalEnvironment.TryResolveVariable("items[]", out IEnumerable<object>? _));
            return Task.CompletedTask;
        });

    private static object?[] ReadSnapshot(IEnvironmentLike environment, string name)
    {
        MSAssert.IsTrue(environment.TryResolveVariable(name, out IEnumerable<object>? collection));
        MSAssert.IsNotNull(collection);
        return [.. collection];
    }
}
