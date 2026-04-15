using Cyborg.Core.Metrics;

namespace Cyborg.Core.Tests.Metrics;

[TestClass]
public sealed class PrometheusBuilderTests
{
    [TestMethod]
    public async Task WriteToAsync_DoesNotEmitUtf8Bom()
    {
        PrometheusBuilder builder = new("test");
        builder.AddSimpleMetric("metric", 1, includeTimeStamp: false);

        using MemoryStream stream = new();
        await builder.WriteToAsync(stream, CancellationToken.None);

        byte[] bytes = stream.ToArray();
        Assert.IsTrue(bytes.Length >= 3, "Output should not be empty.");

        bool hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        Assert.IsFalse(hasBom, "Prometheus .prom output must not start with a UTF-8 BOM (EF BB BF).");
    }
}
