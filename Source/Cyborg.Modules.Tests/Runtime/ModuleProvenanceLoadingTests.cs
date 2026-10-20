using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine;
using Cyborg.Core.Runtime.Model;
using Cyborg.Modules.Configuration.ConfigMap;
using Cyborg.Modules.Dynamic;
using Cyborg.Modules.External;
using Cyborg.Modules.Sequence;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TestAssert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Cyborg.Modules.Tests.Runtime;

[TestClass]
public sealed class ModuleProvenanceLoadingTests : ModuleTestBase
{
    private const string NESTED_CONFIGURATION = """
        {
          "configuration": {
            "cyborg.modules.config.map.v1": {
              "entries": [
                {
                  "key": "template",
                  "cyborg.types.module.reference.v1": {
                    "cyborg.modules.empty.v1": {}
                  }
                }
              ]
            }
          },
          "module": {
            "cyborg.modules.sequence.v1": {
              "steps": [
                {
                  "module": {
                    "cyborg.modules.dynamic.v1": {
                      "target": {
                        "module": {
                          "cyborg.modules.empty.v1": {}
                        }
                      },
                      "tags": []
                    }
                  }
                }
              ]
            }
          }
        }
        """;

    [TestMethod]
    public Task LoadModuleAsync_FileOriginAppliesToAllNestedDefinitionReferencesAsync() => TestWithDIAsync(async services =>
    {
        string path = await WriteTemporaryConfigurationAsync(NESTED_CONFIGURATION);
        try
        {
            IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
            ModuleConfigurationLoadResult loaded = await loader.LoadModuleAsync(path, TestContext.CancellationToken);
            ModuleOrigin origin = new(path);

            TestAssert.AreEqual(origin, loaded.ModuleContext.Module.Origin);
            TestAssert.AreEqual(origin, loaded.ModuleContext.Configuration?.Origin);
            SequenceModule sequence = TestAssert.IsInstanceOfType<SequenceModule>(loaded.ModuleContext.Module.Definition);
            TestAssert.HasCount(1, sequence.Steps);
            ModuleContext nestedContext = sequence.Steps.Single();
            TestAssert.AreEqual(origin, nestedContext.Module.Origin);
            DynamicModule dynamicModule = TestAssert.IsInstanceOfType<DynamicModule>(nestedContext.Module.Definition);
            TestAssert.AreEqual(origin, dynamicModule.Target.Module.Origin);
            ConfigMapModule map = TestAssert.IsInstanceOfType<ConfigMapModule>(loaded.ModuleContext.Configuration?.Definition);
            TestAssert.HasCount(1, map.Entries);
            ModuleReference template = TestAssert.IsInstanceOfType<ModuleReference>(map.Entries.Single().Value);
            TestAssert.AreEqual(origin, template.Origin);
        }
        finally
        {
            File.Delete(path);
        }
    });

    [TestMethod]
    public Task LoadModuleAsync_IndependentFileLoadsDoNotShareContextAsync() => TestWithDIAsync(async services =>
    {
        string firstPath = await WriteTemporaryConfigurationAsync(NESTED_CONFIGURATION);
        string secondPath = await WriteTemporaryConfigurationAsync(NESTED_CONFIGURATION);
        try
        {
            IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
            Task<ModuleConfigurationLoadResult> firstTask = loader.LoadModuleAsync(firstPath, TestContext.CancellationToken);
            Task<ModuleConfigurationLoadResult> secondTask = loader.LoadModuleAsync(secondPath, TestContext.CancellationToken);
            ModuleConfigurationLoadResult[] taskResults = await Task.WhenAll(firstTask, secondTask);
            if (taskResults is not [{ } firstResult, { } secondResult])
            {
                TestAssert.Fail("ModuleConfigurationLoadResult array did not contain two results.");
                return;
            }

            TestAssert.AreEqual(new ModuleOrigin(firstPath), firstResult.ModuleContext.Module.Origin);
            TestAssert.AreEqual(new ModuleOrigin(secondPath), secondResult.ModuleContext.Module.Origin);
            TestAssert.AreNotEqual(firstResult.ModuleContext.Module.Origin, secondResult.ModuleContext.Module.Origin);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    });

    [TestMethod]
    public Task ExecuteAsync_ExternalPathCanResolveRelativeToItsDefinitionFileAsync() => TestWithDIAsync(async services =>
    {
        const string TARGET_JSON = """
            { "module": { "cyborg.modules.empty.v1": {} } }
            """;
        string targetPath = await WriteTemporaryConfigurationAsync(TARGET_JSON);
        string rootJson = $$"""
            {
              "module": {
                "cyborg.modules.external.v1": {
                  "path": "${@.origin.directory}/{{Path.GetFileName(targetPath)}}"
                }
              }
            }
            """;
        string rootPath = await WriteTemporaryConfigurationAsync(rootJson);
        try
        {
            IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
            ModuleConfigurationLoadResult root = await loader.LoadModuleAsync(rootPath, TestContext.CancellationToken);
            IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

            IModuleExecutionResult result = await runtime.ExecuteAsync(root, TestContext.CancellationToken);

            TestAssert.AreEqual(ModuleExitStatus.Success, result.Status);
        }
        finally
        {
            File.Delete(rootPath);
            File.Delete(targetPath);
        }
    });

    [TestMethod]
    public Task LoadModuleAsync_ExternalTargetRetainsItsOwnOriginAsync() => TestWithDIAsync(async services =>
    {
        string targetPath = await WriteTemporaryConfigurationAsync(NESTED_CONFIGURATION);
        string rootJson = $$"""
            {
              "module": {
                "cyborg.modules.external.v1": {
                  "path": {{JsonSerializer.Serialize(targetPath)}}
                }
              }
            }
            """;
        string rootPath = await WriteTemporaryConfigurationAsync(rootJson);
        try
        {
            IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
            ModuleConfigurationLoadResult root = await loader.LoadModuleAsync(rootPath, TestContext.CancellationToken);
            ModuleConfigurationLoadResult target = await loader.LoadModuleAsync(targetPath, TestContext.CancellationToken);

            TestAssert.AreEqual(new ModuleOrigin(rootPath), root.ModuleContext.Module.Origin);
            TestAssert.IsInstanceOfType<ExternalModule>(root.ModuleContext.Module.Definition);
            TestAssert.AreEqual(new ModuleOrigin(targetPath), target.ModuleContext.Module.Origin);
        }
        finally
        {
            File.Delete(rootPath);
            File.Delete(targetPath);
        }
    });

    private static async Task<string> WriteTemporaryConfigurationAsync(string json)
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
