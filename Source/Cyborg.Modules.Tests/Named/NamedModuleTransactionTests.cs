using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Modules.Tests.Named;

[TestClass]
public sealed class NamedModuleTransactionTests : ModuleTestBase
{
    private const string NAMED_SEQUENCE = """
        {
          "module": {
            "cyborg.modules.sequence.v1": {
              "steps": [
                {
                  "module": {
                    "cyborg.modules.empty.v1": {
                      "name": "shared"
                    }
                  }
                },
                {
                  "module": {
                    "cyborg.modules.named.ref.v1": {
                      "target": "shared"
                    }
                  }
                }
              ]
            }
          }
        }
        """;

    [TestMethod]
    public async Task ExecuteAsync_StaticNamedModulesAreSeededIntoExecutionTransactionAsync()
    {
        await TestWithDIAsync(async services =>
        {
            string path = await WriteTemporaryConfigurationAsync(NAMED_SEQUENCE);
            try
            {
                IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
                ModuleContext moduleContext = await loader.LoadModuleAsync(path, TestContext.CancellationToken);
                IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

                IModuleExecutionResult result = await runtime.ExecuteAsync(moduleContext, TestContext.CancellationToken);

                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(ModuleExitStatus.Success, result.Status);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    [TestMethod]
    public async Task ExecuteAsync_DynamicallyLoadedNamedModulesAreVisibleInsideDynamicTransactionAsync()
    {
        await TestWithDIAsync(async services =>
        {
            string nestedPath = await WriteTemporaryConfigurationAsync(NAMED_SEQUENCE);
            string externalJson = $$"""
                {
                  "module": {
                    "cyborg.modules.external.v1": {
                      "path": {{System.Text.Json.JsonSerializer.Serialize(nestedPath)}}
                    }
                  }
                }
                """;
            string rootPath = await WriteTemporaryConfigurationAsync(externalJson);
            try
            {
                IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
                ModuleContext moduleContext = await loader.LoadModuleAsync(rootPath, TestContext.CancellationToken);
                IModuleRuntime runtime = services.GetRequiredService<IModuleRuntime>();

                IModuleExecutionResult result = await runtime.ExecuteAsync(moduleContext, TestContext.CancellationToken);

                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(ModuleExitStatus.Success, result.Status);
            }
            finally
            {
                File.Delete(rootPath);
                File.Delete(nestedPath);
            }
        });
    }

    [TestMethod]
    public async Task ExecuteAsync_NamedModuleStatePersistsWithinRootButDoesNotLeakAcrossRootsAsync()
    {
        await TestWithDIAsync(async services =>
        {
            string seedPath = await WriteTemporaryConfigurationAsync(NAMED_SEQUENCE);
            const string referenceJson = """
                {
                  "module": {
                    "cyborg.modules.named.ref.v1": {
                      "target": "shared"
                    }
                  }
                }
                """;
            string referencePath = await WriteTemporaryConfigurationAsync(referenceJson);
            try
            {
                IModuleConfigurationLoader loader = services.GetRequiredService<IModuleConfigurationLoader>();
                ModuleContext seedContext = await loader.LoadModuleAsync(seedPath, TestContext.CancellationToken);
                ModuleContext referenceContext = await loader.LoadModuleAsync(referencePath, TestContext.CancellationToken);
                IModuleRuntime firstRoot = services.GetRequiredService<IModuleRuntime>();
                IModuleRuntime secondRoot = services.GetRequiredService<IModuleRuntime>();

                IModuleExecutionResult seedResult = await firstRoot.ExecuteAsync(seedContext, TestContext.CancellationToken);
                IModuleExecutionResult repeatedResult = await firstRoot.ExecuteAsync(referenceContext, TestContext.CancellationToken);

                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(ModuleExitStatus.Success, seedResult.Status);
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(ModuleExitStatus.Success, repeatedResult.Status);
                IModuleExecutionResult isolatedResult = await secondRoot.ExecuteAsync(referenceContext, TestContext.CancellationToken);
                Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual(ModuleExitStatus.Failed, isolatedResult.Status);
            }
            finally
            {
                File.Delete(referencePath);
                File.Delete(seedPath);
            }
        });
    }

    private static async Task<string> WriteTemporaryConfigurationAsync(string json)
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
