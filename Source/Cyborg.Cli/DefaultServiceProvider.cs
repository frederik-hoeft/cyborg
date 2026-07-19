using Cyborg.Cli.Arguments;
using Cyborg.Core;
using Cyborg.Core.Modules.Descriptors.Builders;
using Cyborg.Core.Modules.Descriptors.Model;
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
[Transient<IObjectDescriptionBuilder, ObjectDescriptionBuilder>]
[Singleton<IDescriptionComponentFactory, DefaultDescriptionComponentFactory>]
[Singleton<IEnvironmentVariableArgumentHandler, EnvironmentVariableArgumentHandler>]
[Singleton<JsonSerializerContext>(Factory = nameof(GetCliJsonSerializerContext))]
internal sealed partial class DefaultServiceProvider
{
    private static CliJsonSerializerContext GetCliJsonSerializerContext() => CliJsonSerializerContext.Default;
}
