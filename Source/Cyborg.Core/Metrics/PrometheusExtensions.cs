namespace Cyborg.Core.Metrics;

internal static class PrometheusExtensions
{
    public static string ToPrometheusString(this PrometheusMetricType type) => type switch
    {
        PrometheusMetricType.Untyped => "untyped",
        PrometheusMetricType.Counter => "counter",
        PrometheusMetricType.Gauge => "gauge",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static string ToPrometheusString(this double value) => value switch
    {
        double.NaN => "NaN",
        double.PositiveInfinity => "+Inf",
        double.NegativeInfinity => "-Inf",
        _ => value.ToString(),
    };
}