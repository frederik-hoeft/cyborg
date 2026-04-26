using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Cli.Logging;

internal sealed class DynamicGlobalLoggingOptionsProvider() : DynamicValueProviderBase<GlobalLoggingOptions>("cyborg.types.services.logging.v1");
