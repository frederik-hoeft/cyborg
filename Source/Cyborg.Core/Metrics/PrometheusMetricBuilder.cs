using SmartmonExporter.Domain.Metrics;
using System.Text;

namespace Cyborg.Core.Metrics;

internal sealed class PrometheusMetricBuilder(PrometheusMetric metric, bool includeTimeStamp)
{
    public PrometheusMetricBuilder AddSample(bool value, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSample(value ? "1" : "0", labels);

    public PrometheusMetricBuilder AddSample(float value, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSample((double)value, labels);

    public PrometheusMetricBuilder AddSample(long value, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSample(value.ToString(), labels);

    public PrometheusMetricBuilder AddSample(int value, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSample(value.ToString(), labels);

    public PrometheusMetricBuilder AddSample(double value, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSample(value.ToPrometheusString(), labels);

    internal PrometheusMetricBuilder AddSample(string value, params ReadOnlySpan<PrometheusLabel?> labels)
    {
        StringBuilder builder = metric._builder;
        builder.Append(metric.Namespace).Append('_').Append(metric.Name);
        
        // Count non-null labels
        int nonNullCount = 0;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i].HasValue)
            {
                nonNullCount++;
            }
        }
        
        if (nonNullCount > 0)
        {
            builder.Append('{');
            bool first = true;
            for (int i = 0; i < labels.Length; i++)
            {
                PrometheusLabel? nullableLabel = labels[i];
                if (!nullableLabel.HasValue)
                {
                    continue;
                }
                
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                
                (string label, string labelValue) = nullableLabel.Value;
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
