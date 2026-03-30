using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicRollingFileLoggingConfiguratorOptionsProvider() : DynamicValueProviderBase<RollingFileLoggingConfiguratorOptions>("cyborg.types.services.logging.rolling.v1");