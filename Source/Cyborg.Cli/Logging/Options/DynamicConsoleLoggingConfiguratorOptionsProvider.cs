using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicConsoleLoggingConfiguratorOptionsProvider() : DynamicValueProviderBase<ConsoleLoggingConfiguratorOptions>("cyborg.types.services.logging.console.v1");