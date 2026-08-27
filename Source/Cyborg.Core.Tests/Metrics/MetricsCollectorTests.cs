using Cyborg.Core.Services.Metrics;
using Cyborg.Core.Text.Rendering;
using System.Text;

namespace Cyborg.Core.Tests.Metrics;

[TestClass]
public sealed class MetricsCollectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddGauge_ConcurrentWritersProduceCompleteSnapshotAsync()
    {
        const int SAMPLE_COUNT = 256;
        MetricsCollector collector = new(
            new MetricsCollectorOptions { Namespace = "test" },
            new DefaultTaggedStringRenderer([]));
        Task[] writers = new Task[SAMPLE_COUNT];
        for (int i = 0; i < writers.Length; i++)
        {
            int sample = i;
            writers[i] = Task.Run(() => collector.AddGauge(
                "parallel_metric",
                "Parallel metric",
                samples => samples.Add(
                    sample,
                    collector.CreateLabels().AddLabel("worker", sample.ToString()))));
        }
        await Task.WhenAll(writers);

        using MemoryStream stream = new();
        await collector.WriteToAsync(stream, TestContext.CancellationToken);
        string output = Encoding.UTF8.GetString(stream.ToArray());
        string[] samples = [.. output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(static line => line.StartsWith("test_parallel_metric{", StringComparison.Ordinal))];

        Assert.HasCount(SAMPLE_COUNT, samples);
        Assert.AreEqual(SAMPLE_COUNT, samples.Distinct(StringComparer.Ordinal).Count());
    }
}
