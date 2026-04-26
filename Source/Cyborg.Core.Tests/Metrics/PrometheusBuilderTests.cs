using Cyborg.Core.Metrics;

namespace Cyborg.Core.Tests.Metrics;

[TestClass]
public sealed class PrometheusBuilderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task WriteToAsync_DoesNotEmitUtf8BomAsync()
    {
        PrometheusBuilder builder = new("test");
        builder.AddSimpleMetric("metric", 1, includeTimeStamp: false);

        using MemoryStream stream = new();
        await builder.WriteToAsync(stream, TestContext.CancellationToken);

        byte[] bytes = stream.ToArray();
        Assert.IsNotEmpty(bytes, "Output should not be empty.");

        bool hasBom = bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF;
        Assert.IsFalse(hasBom, "Prometheus .prom output must not start with a UTF-8 BOM (EF BB BF).");
    }
}
