using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicFileLoggingConfiguratorOptionsProvider() : DynamicValueProviderBase<FileLoggingConfiguratorOptions>("cyborg.types.services.logging.file.v1");