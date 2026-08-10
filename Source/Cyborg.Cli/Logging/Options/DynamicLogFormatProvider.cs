using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicLogFormatProvider() : DynamicEnumValueProvider<LogFormat>("cyborg.types.services.logging.format.v1");
