using Cyborg.Core.Metrics;

namespace Cyborg.Core.Services.Metrics;

internal sealed class MetricSampleCollection(PrometheusMetricBuilder builder) : IMetricSampleCollection
{
    public IMetricSampleCollection Add(bool sample, Action<IMetricsLabelCollection> buildLabels) =>
        Add(sample, MaterializeLabels(buildLabels));

    public IMetricSampleCollection Add(bool sample, IMetricsLabelCollection labels)
    {
        builder.AddSample(sample, labels.GetLabels());
        return this;
    }

    public IMetricSampleCollection Add(float sample, Action<IMetricsLabelCollection> buildLabels) =>
        Add(sample, MaterializeLabels(buildLabels));

    public IMetricSampleCollection Add(float sample, IMetricsLabelCollection labels)
    {
        builder.AddSample(sample, labels.GetLabels());
        return this;
    }

    public IMetricSampleCollection Add(long sample, Action<IMetricsLabelCollection> buildLabels) =>
        Add(sample, MaterializeLabels(buildLabels));

    public IMetricSampleCollection Add(long sample, IMetricsLabelCollection labels)
    {
        builder.AddSample(sample, labels.GetLabels());
        return this;
    }

    public IMetricSampleCollection Add(int sample, Action<IMetricsLabelCollection> buildLabels) =>
        Add(sample, MaterializeLabels(buildLabels));

    public IMetricSampleCollection Add(int sample, IMetricsLabelCollection labels)
    {
        builder.AddSample(sample, labels.GetLabels());
        return this;
    }

    public IMetricSampleCollection Add(double sample, Action<IMetricsLabelCollection> buildLabels) =>
        Add(sample, MaterializeLabels(buildLabels));

    public IMetricSampleCollection Add(double sample, IMetricsLabelCollection labels)
    {
        builder.AddSample(sample, labels.GetLabels());
        return this;
    }

    public IMetricSampleCollection Add(string sample, Action<IMetricsLabelCollection> buildLabels) =>
        Add(sample, MaterializeLabels(buildLabels));

    public IMetricSampleCollection Add(string sample, IMetricsLabelCollection labels)
    {
        builder.AddSample(sample, labels.GetLabels());
        return this;
    }

    private static MetricsLabelCollection MaterializeLabels(Action<IMetricsLabelCollection> buildLabels)
    {
        MetricsLabelCollection labels = new();
        buildLabels(labels);
        return labels;
    }
}
