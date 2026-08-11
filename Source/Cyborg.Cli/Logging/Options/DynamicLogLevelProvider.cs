using Cyborg.Core.Configuration.Serialization.Dynamics.Providers;
using Microsoft.Extensions.Logging;

namespace Cyborg.Cli.Logging.Options;

internal sealed class DynamicLogLevelProvider() : DynamicEnumValueProvider<LogLevel>("cyborg.types.services.logging.level.v1");
