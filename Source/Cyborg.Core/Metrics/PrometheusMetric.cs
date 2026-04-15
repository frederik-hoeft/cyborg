using System.Text;

namespace Cyborg.Core.Metrics;

internal sealed class PrometheusMetric(string prometheusNamespace, string name, PrometheusMetricTypeDescriptor? type = default)
{
    internal readonly StringBuilder _builder = new();

    public string Namespace { get; } = prometheusNamespace;

    public string Name { get; } = name;

    public PrometheusMetricTypeDescriptor? Type { get; private set; } = type;

    public PrometheusMetricBuilder CreateBuilder(PrometheusMetricTypeDescriptor? type, bool includeTimeStamp)
    {
        if (!Type.HasValue)
        {
            Type = type;
        }
        else if (Type != type)
        {
            throw new InvalidOperationException($"Metric type mismatch for {Namespace}_{Name}");
        }
        return new PrometheusMetricBuilder(this, includeTimeStamp);
    }

    public void WriteTo(StringBuilder builder)
    {
        if (Type.HasValue)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }
            builder.Append("# HELP ").Append(Namespace).Append('_').Append(Name).Append(' ').AppendLine(Escape(Type.Value.Description));
            builder.Append("# TYPE ").Append(Namespace).Append('_').Append(Name).Append(' ').AppendLine(Type.Value.Type.ToPrometheusString());
        }
        builder.Append(_builder);
    }

    internal static string Escape(string text) => text.Replace("\"", "\\\"");
}