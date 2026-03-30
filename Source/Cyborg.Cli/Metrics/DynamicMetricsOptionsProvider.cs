using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Cli.Metrics;

internal sealed class DynamicMetricsOptionsProvider() : DynamicValueProviderBase<MetricsOptions>("cyborg.types.services.metrics.v1");