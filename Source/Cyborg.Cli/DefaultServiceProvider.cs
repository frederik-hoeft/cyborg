using Cyborg.Core;
using Cyborg.Modules;
using Jab;

namespace Cyborg.Cli;

[ServiceProvider]
[Import<ICyborgCoreServices>]
[Import<ICyborgModuleServices>]
internal sealed partial class DefaultServiceProvider;