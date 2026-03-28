using Cyborg.Core;
using Cyborg.Modules;
using Cyborg.Modules.Borg;
using Jab;

namespace Cyborg.Cli;

[ServiceProvider]
[Import<ICyborgCoreServices>]
[Import<ICyborgModuleServices>]
[Import<ICyborgBorgServices>]
[Import<ICyborgCliLoggingServices>]
internal sealed partial class DefaultServiceProvider;