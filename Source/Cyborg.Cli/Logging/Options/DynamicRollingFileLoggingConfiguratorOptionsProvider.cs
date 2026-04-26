using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicRollingFileLoggingConfiguratorOptionsProvider() : DynamicValueProviderBase<RollingFileLoggingConfiguratorOptions>("cyborg.types.services.logging.rolling.v1");
