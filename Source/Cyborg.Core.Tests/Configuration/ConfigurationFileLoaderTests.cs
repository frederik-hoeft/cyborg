using Cyborg.Core.Configuration;
using Cyborg.Core.Configuration.Loaders;
using Microsoft.Extensions.DependencyInjection;

namespace Cyborg.Core.Tests.Configuration;

[TestClass]
public sealed class ConfigurationFileLoaderTests : CyborgCoreTestBase
{
    [TestMethod]
    public async Task Test_LoadSourcesAsync_PreservesInsertionOrderAndDeduplicatesPathsAsync()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"cyborg-config-loader-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string firstFile = Path.Combine(tempDirectory, "first.jconf");
            string secondFile = Path.Combine(tempDirectory, "second.jconf");
            await File.WriteAllTextAsync(firstFile, CreateStringConfiguration("first"), TestContext.CancellationToken);
            await File.WriteAllTextAsync(secondFile, CreateStringConfiguration("second"), TestContext.CancellationToken);

            await TestWithDIAsync(async services =>
            {
                IConfigurationFileLoader loader = services.GetRequiredService<IConfigurationFileLoader>();
                loader.Add(firstFile).Add(secondFile).Add(firstFile);

                List<IConfigurationSource> sources = [];
                await foreach (IConfigurationSource source in loader.LoadSourcesAsync(TestContext.CancellationToken))
                {
                    sources.Add(source);
                }

                Assert.HasCount(2, sources);
                Assert.AreEqual("first", sources[0].Options.Single().Value);
                Assert.AreEqual("second", sources[1].Options.Single().Value);
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateStringConfiguration(string value) => $$"""
        {
          "options": [
            {
              "key": "test.value",
              "string": "{{value}}"
            }
          ]
        }
        """;
}
