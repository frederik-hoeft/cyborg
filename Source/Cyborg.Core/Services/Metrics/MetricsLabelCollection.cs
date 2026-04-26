using Cyborg.Core.Metrics;
using Cyborg.Core.Metrics.Factory;

namespace Cyborg.Core.Services.Metrics;

internal sealed class MetricsLabelCollection : IMetricsLabelCollection
{
    private readonly List<PrometheusLabel> _labels = [];

    public IMetricsLabelCollection Add(IMetricsLabelCollection labels)
    {
        _labels.AddRange(labels.GetLabels());
        return this;
    }

    public IMetricsLabelCollection AddLabel(string name, string value)
    {
        _labels.Add(Prometheus.Label(name, value));
        return this;
    }

    IReadOnlyList<PrometheusLabel> IMetricsLabelCollection.GetLabels() => _labels;
}
