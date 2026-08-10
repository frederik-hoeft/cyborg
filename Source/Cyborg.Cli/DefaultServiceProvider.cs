using Cyborg.Cli.Arguments;
using Cyborg.Cli.Configuration;
using Cyborg.Cli.Debugging;
using Cyborg.Core;
using Cyborg.Modules;
using Cyborg.Modules.Borg;
using Jab;
using System.Text.Json.Serialization;

namespace Cyborg.Cli;

[ServiceProvider]
[Import<ICyborgCoreServices>]
[Import<ICyborgModuleServices>]
[Import<ICyborgBorgServices>]
[Import<ICyborgCliServiceOptions>]
[Import<ICyborgCliDebugServices>]
[Singleton<IEnvironmentVariableArgumentHandler, EnvironmentVariableArgumentHandler>]
[Singleton<IConfigurationArgumentHandler, ConfigurationArgumentHandler>]
[Singleton<ICliConfigurationService, CliConfigurationService>]
[Singleton<JsonSerializerContext>(Factory = nameof(GetCliJsonSerializerContext))]
internal sealed partial class DefaultServiceProvider
{
    internal static CliJsonSerializerContext GetCliJsonSerializerContext() => CliJsonSerializerContext.Default;
}
