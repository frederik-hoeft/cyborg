namespace Cyborg.Core.Services.Metrics;

public interface IMetricsCollector
{
    IMetricsLabelCollection CreateLabels();

    void AddUntyped(string metricName, string description, Action<IMetricSampleCollection> buildSamples);

    void AddCounter(string metricName, string description, Action<IMetricSampleCollection> buildSamples);

    void AddGauge(string metricName, string description, Action<IMetricSampleCollection> buildSamples);

    Task WriteToAsync(Stream outputStream, CancellationToken cancellationToken);
}
