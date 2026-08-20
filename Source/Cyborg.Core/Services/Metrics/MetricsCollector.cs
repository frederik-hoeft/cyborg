using Cyborg.Core.Metrics;
using Cyborg.Core.Metrics.Factory;
using Cyborg.Core.Text.Rendering;

namespace Cyborg.Core.Services.Metrics;

public sealed class MetricsCollector(MetricsCollectorOptions options, ITaggedStringRenderer taggedStringRenderer) : IMetricsCollector
{
    private readonly PrometheusBuilder _builder = new(options.Namespace);

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
        PrometheusMetricBuilder builder = _builder.GetMetricBuilder(metricName, type, options.IncludeTimeStamp);
        MetricSampleCollection samples = new(builder, taggedStringRenderer);
        buildSamples(samples);
    }

    public Task WriteToAsync(Stream outputStream, CancellationToken cancellationToken) =>
        _builder.WriteToAsync(outputStream, cancellationToken);
}
