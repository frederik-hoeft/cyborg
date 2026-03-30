using Cyborg.Cli.Logging;
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
[Import<ICyborgCliLoggingServices>]
[Singleton<JsonSerializerContext>(Factory = nameof(GetCliJsonSerializerContext))]
internal sealed partial class DefaultServiceProvider
{
    private static CliJsonSerializerContext GetCliJsonSerializerContext() => CliJsonSerializerContext.Default;
}