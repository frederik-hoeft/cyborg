using Cyborg.Core.Modules.Configuration.Serialization.Dynamics.ValueProviders;

namespace Cyborg.Cli.Logging;

internal sealed class DynamicGlobalLoggingOptionsProvider() : DynamicValueProviderBase<GlobalLoggingOptions>("cyborg.types.services.logging.v1");
