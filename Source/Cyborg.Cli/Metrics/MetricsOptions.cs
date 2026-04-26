using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.Cli.Metrics;

[GeneratedDecomposition]
internal sealed partial record MetricsOptions
(
    string Namespace = "cyborg",
    string FilePath = "/var/log/cyborg/metrics.prom"
);
