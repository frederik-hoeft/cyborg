using Cyborg.Core.Metrics;
using Cyborg.Core.Metrics.Factory;
using Cyborg.Core.Text;
using Cyborg.Core.Text.Rendering;

namespace Cyborg.Core.Services.Metrics;

internal sealed class MetricsLabelCollection(ITaggedStringRenderer taggedStringRenderer) : IMetricsLabelCollection
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

    public IMetricsLabelCollection AddLabel(string name, TaggedString value) =>
        AddLabel(name, taggedStringRenderer.Render(value));

    IReadOnlyList<PrometheusLabel> IMetricsLabelCollection.GetLabels() => _labels;
}
