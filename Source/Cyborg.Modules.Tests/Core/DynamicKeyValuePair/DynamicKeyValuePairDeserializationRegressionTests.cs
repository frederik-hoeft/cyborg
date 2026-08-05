using Cyborg.Modules.Configuration.ConfigMap;
using System.Text.Json;

namespace Cyborg.Modules.Tests.Core.DynamicKeyValuePair;

[TestClass]
public sealed class DynamicKeyValuePairDeserializationRegressionTests : ModuleTestBase
{
    [TestMethod]
    public async Task TestDeserializationAsync_MissingDynamicKey_ThrowsJsonExceptionAsync()
    {
        const string JSON =
            """
            {
              "cyborg.modules.config.map.v1": {
                "entries": [
                  { "string": "value" }
                ]
              }
            }
            """;

        await MSAssert.ThrowsExactlyAsync<JsonException>(
            () => TestDeserializationAsync<ConfigMapModule>(JSON, static _ => { }));
    }

    [TestMethod]
    public async Task TestDeserializationAsync_MissingDynamicValue_ThrowsJsonExceptionAsync()
    {
        const string JSON =
            """
            {
              "cyborg.modules.config.map.v1": {
                "entries": [
                  { "key": "value" }
                ]
              }
            }
            """;

        await MSAssert.ThrowsExactlyAsync<JsonException>(
            () => TestDeserializationAsync<ConfigMapModule>(JSON, static _ => { }));
    }
}
