using Cyborg.Core.Metrics;
using Cyborg.Core.Metrics.Factory;
using Cyborg.Core.Text.Rendering;
using System.Text;

namespace Cyborg.Core.Services.Metrics;

public sealed class MetricsCollector(MetricsCollectorOptions options, ITaggedStringRenderer taggedStringRenderer) : IMetricsCollector
{
    private static readonly Encoding s_utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly PrometheusBuilder _builder = new(options.Namespace);
    private readonly Lock _syncRoot = new();

    public IMetricsLabelCollection CreateLabels() => new MetricsLabelCollection(taggedStringRenderer);

    public void AddCounter(string metricName, string description, Action<IMetricSampleCollection> buildSamples) =>
        AddMetric(metricName, Prometheus.Counter(description), buildSamples);

    public void AddGauge(string metricName, string description, Action<IMetricSampleCollection> buildSamples) =>
        AddMetric(metricName, Prometheus.Gauge(description), buildSamples);

    public void AddUntyped(string metricName, string description, Action<IMetricSampleCollection> buildSamples) =>
        AddMetric(metricName, Prometheus.Untyped(description), buildSamples);

    private void AddMetric(string metricName, PrometheusMetricTypeDescriptor type, Action<IMetricSampleCollection> buildSamples)
    {
        ArgumentNullException.ThrowIfNull(metricName);
        ArgumentNullException.ThrowIfNull(buildSamples);
        lock (_syncRoot)
        {
            PrometheusMetricBuilder builder = _builder.GetMetricBuilder(metricName, type, options.IncludeTimeStamp);
            MetricSampleCollection samples = new(builder, taggedStringRenderer);
            buildSamples(samples);
        }
    }

    public async Task WriteToAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        string snapshot;
        lock (_syncRoot)
        {
            snapshot = _builder.Build();
        }
        using StreamWriter writer = new(outputStream, s_utf8NoBom, leaveOpen: true);
        await writer.WriteAsync(snapshot.AsMemory(), cancellationToken);
    }
}
