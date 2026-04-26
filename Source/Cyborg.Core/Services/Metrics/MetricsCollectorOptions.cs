namespace Cyborg.Core.Services.Metrics;

public sealed class MetricsCollectorOptions
{
    public string Namespace { get; set; } = null!;

    public bool IncludeTimeStamp { get; set; } = false;
}
