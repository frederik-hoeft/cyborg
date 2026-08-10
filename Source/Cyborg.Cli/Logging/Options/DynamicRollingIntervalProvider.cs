using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;
using ZLogger.Providers;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicRollingIntervalProvider() : DynamicEnumValueProvider<RollingInterval>("cyborg.types.services.logging.rolling_interval.v1");
