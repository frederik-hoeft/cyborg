using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicFileLoggingConfiguratorOptionsProvider() : DynamicValueProviderBase<FileLoggingConfiguratorOptions>("cyborg.types.services.logging.file.v1");