namespace Cyborg.Core.Services.Metrics;

public interface IMetricSampleCollection
{
    IMetricSampleCollection Add(bool sample, Action<IMetricsLabelCollection> buildLabels);

    IMetricSampleCollection Add(bool sample, IMetricsLabelCollection labels);

    IMetricSampleCollection Add(float sample, Action<IMetricsLabelCollection> buildLabels);

    IMetricSampleCollection Add(float sample, IMetricsLabelCollection labels);

    IMetricSampleCollection Add(long sample, Action<IMetricsLabelCollection> buildLabels);

    IMetricSampleCollection Add(long sample, IMetricsLabelCollection labels);

    IMetricSampleCollection Add(int sample, Action<IMetricsLabelCollection> buildLabels);

    IMetricSampleCollection Add(int sample, IMetricsLabelCollection labels);

    IMetricSampleCollection Add(double sample, Action<IMetricsLabelCollection> buildLabels);

    IMetricSampleCollection Add(double sample, IMetricsLabelCollection labels);

    IMetricSampleCollection Add(string sample, Action<IMetricsLabelCollection> buildLabels);

    IMetricSampleCollection Add(string sample, IMetricsLabelCollection labels);
}
