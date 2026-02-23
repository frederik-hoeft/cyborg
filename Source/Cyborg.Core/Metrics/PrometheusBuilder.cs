using Cyborg.Core.Metrics;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartmonExporter.Domain.Metrics;

internal sealed partial class PrometheusBuilder
{
    private readonly StringBuilder _builder;
    private readonly Dictionary<string, PrometheusMetric> _metrics = [];
    private readonly string _prometheusNamespace;

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    internal static partial Regex PrometheusNameRegex { get; }

    public PrometheusBuilder(string prometheusNamespace, int capacity = -1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prometheusNamespace, nameof(prometheusNamespace));
        if (!PrometheusNameRegex.IsMatch(prometheusNamespace))
        {
            throw new ArgumentException("Invalid prometheus namespace", nameof(prometheusNamespace));
        }

        _prometheusNamespace = prometheusNamespace;
        _builder = capacity == -1 ? new() : new(capacity);
    }

    public PrometheusBuilder AddSimpleMetric(string name, bool value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSimpleMetric(name, value ? "1" : "0", includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, float value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSimpleMetric(name, (double)value, includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, long value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSimpleMetric(name, value.ToString(), includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, int value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSimpleMetric(name, value.ToString(), includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, double value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel?> labels) =>
        AddSimpleMetric(name, value.ToPrometheusString(), includeTimeStamp, labels);

    private PrometheusBuilder AddSimpleMetric(string name, string value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel?> labels)
    {
        if (!PrometheusNameRegex.IsMatch(name))
        {
            throw new ArgumentException("Invalid prometheus metric name", nameof(name));
        }
        if (!_metrics.TryGetValue(name, out PrometheusMetric? metric))
        {
            metric = new PrometheusMetric(_prometheusNamespace, name);
            _metrics[name] = metric;
        }
        PrometheusMetricBuilder builder = metric.CreateBuilder(null, includeTimeStamp);
        builder.AddSample(value, labels);
        return this;
    }

    public PrometheusBuilder AddMetric(string name, PrometheusMetricTypeDescriptor type, bool includeTimeStamp, Action<PrometheusMetricBuilder> addSamples)
    {
        if (!PrometheusNameRegex.IsMatch(name))
        {
            throw new ArgumentException("Invalid prometheus metric name", nameof(name));
        }
        if (!_metrics.TryGetValue(name, out PrometheusMetric? metric))
        {
            metric = new PrometheusMetric(_prometheusNamespace, name, type);
            _metrics[name] = metric;
        }
        PrometheusMetricBuilder builder = metric.CreateBuilder(type, includeTimeStamp);
        addSamples(builder);
        return this;
    }

    public string Build()
    {
        foreach (PrometheusMetric metric in _metrics.Values.OrderBy(metric => metric.Name))
        {
            metric.WriteTo(_builder);
        }
        return _builder.ToString();
    }
}
