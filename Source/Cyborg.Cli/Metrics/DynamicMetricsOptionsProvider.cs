using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Cli.Metrics;

internal sealed class DynamicMetricsOptionsProvider() : DynamicValueProviderBase<MetricsOptions>("cyborg.types.services.metrics.v1");
