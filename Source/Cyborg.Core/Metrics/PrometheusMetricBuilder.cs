using System.Text;

namespace Cyborg.Core.Metrics;

internal sealed class PrometheusMetricBuilder(PrometheusMetric metric, bool includeTimeStamp)
{
    public PrometheusMetricBuilder AddSample(bool value, IReadOnlyList<PrometheusLabel> labels) =>
        AddSample(value ? "1" : "0", labels);

    public PrometheusMetricBuilder AddSample(float value, IReadOnlyList<PrometheusLabel> labels) =>
        AddSample((double)value, labels);

    public PrometheusMetricBuilder AddSample(long value, IReadOnlyList<PrometheusLabel> labels) =>
        AddSample(value.ToString(), labels);

    public PrometheusMetricBuilder AddSample(int value, IReadOnlyList<PrometheusLabel> labels) =>
        AddSample(value.ToString(), labels);

    public PrometheusMetricBuilder AddSample(double value, IReadOnlyList<PrometheusLabel> labels) =>
        AddSample(value.ToPrometheusString(), labels);

    internal PrometheusMetricBuilder AddSample(string value, IReadOnlyList<PrometheusLabel> labels)
    {
        StringBuilder builder = metric._builder;
        builder.Append(metric.Namespace).Append('_').Append(metric.Name);
        
        if (labels.Count > 0)
        {
            builder.Append('{');
            bool first = true;
            for (int i = 0; i < labels.Count; i++)
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                
                (string label, string labelValue) = labels[i];
                if (!PrometheusBuilder.PrometheusNameRegex.IsMatch(label))
                {
                    throw new ArgumentException($"Invalid label name '{label}'");
                }
                string escapedLabelValue = PrometheusMetric.Escape(labelValue);
                builder.Append(label).Append("=\"").Append(escapedLabelValue).Append('"');
            }
            builder.Append('}');
        }
        builder.Append(' ').Append(value);
        if (includeTimeStamp)
        {
            builder.Append(' ').Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        builder.AppendLine();
        return this;
    }
}
