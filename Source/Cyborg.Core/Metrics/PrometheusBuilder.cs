using System.Text;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Metrics;

internal sealed partial class PrometheusBuilder
{
    private readonly int _initialCapacity;
    private readonly string _prometheusNamespace;

    internal Dictionary<string, PrometheusMetric> Metrics { get; } = [];

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
        _initialCapacity = capacity;
    }

    public PrometheusBuilder AddSimpleMetric(string name, bool value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel> labels) =>
        AddSimpleMetric(name, value ? "1" : "0", includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, float value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel> labels) =>
        AddSimpleMetric(name, (double)value, includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, long value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel> labels) =>
        AddSimpleMetric(name, value.ToString(), includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, int value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel> labels) =>
        AddSimpleMetric(name, value.ToString(), includeTimeStamp, labels);

    public PrometheusBuilder AddSimpleMetric(string name, double value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel> labels) =>
        AddSimpleMetric(name, value.ToPrometheusString(), includeTimeStamp, labels);

    private PrometheusBuilder AddSimpleMetric(string name, string value, bool includeTimeStamp, params ReadOnlySpan<PrometheusLabel> labels)
    {
        PrometheusMetricBuilder builder = GetMetricBuilder(name, null, includeTimeStamp);
        builder.AddSample(value, labels.ToArray());
        return this;
    }

    internal PrometheusMetricBuilder GetMetricBuilder(string name, PrometheusMetricTypeDescriptor? type, bool includeTimeStamp)
    {
        if (!PrometheusNameRegex.IsMatch(name))
        {
            throw new ArgumentException("Invalid prometheus metric name", nameof(name));
        }
        if (!Metrics.TryGetValue(name, out PrometheusMetric? metric))
        {
            metric = new PrometheusMetric(_prometheusNamespace, name, type);
            Metrics[name] = metric;
        }
        return metric.CreateBuilder(type, includeTimeStamp);
    }

    public PrometheusBuilder AddMetric(string name, PrometheusMetricTypeDescriptor type, bool includeTimeStamp, Action<PrometheusMetricBuilder> addSamples)
    {
        PrometheusMetricBuilder builder = GetMetricBuilder(name, type, includeTimeStamp);
        addSamples(builder);
        return this;
    }

    private StringBuilder Finalize()
    {
        StringBuilder builder = _initialCapacity > 0 ? new StringBuilder(_initialCapacity) : new StringBuilder();
        foreach (PrometheusMetric metric in Metrics.Values.OrderBy(metric => metric.Name))
        {
            metric.WriteTo(builder);
        }
        return builder;
    }

    public string Build() => Finalize().ToString();

    public async Task WriteToAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        StringBuilder builder = Finalize();
        using StreamWriter writer = new(outputStream, Encoding.UTF8, leaveOpen: true);
        await writer.WriteAsync(builder, cancellationToken);
    }
}
