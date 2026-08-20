using Cyborg.Core.Metrics;
using Cyborg.Core.Text;

namespace Cyborg.Core.Services.Metrics;

public interface IMetricsLabelCollection
{
    IMetricsLabelCollection AddLabel(string name, string value);

    IMetricsLabelCollection AddLabel(string name, TaggedString value);

    IMetricsLabelCollection Add(IMetricsLabelCollection labels);

    internal IReadOnlyList<PrometheusLabel> GetLabels();
}
