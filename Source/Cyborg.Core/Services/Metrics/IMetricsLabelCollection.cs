using Cyborg.Core.Metrics;

namespace Cyborg.Core.Services.Metrics;

public interface IMetricsLabelCollection
{
    IMetricsLabelCollection AddLabel(string name, string value);

    IMetricsLabelCollection Add(IMetricsLabelCollection labels);

    internal IReadOnlyList<PrometheusLabel> GetLabels();
}
