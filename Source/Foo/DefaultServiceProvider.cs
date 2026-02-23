
using Cyborg.Core.Modules;
using Jab;

namespace Foo;

[ServiceProvider]
[Import<ICyborgCoreModule>]
internal sealed partial class DefaultServiceProvider;