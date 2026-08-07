using Cyborg.Core.Modules.Descriptors;
using Jab;

namespace Cyborg.Core.Tests.Debugging;

[ServiceProvider]
[Import<IModuleDescriptionServices>]
internal sealed partial class DescriptionTestServiceProvider
{
}
