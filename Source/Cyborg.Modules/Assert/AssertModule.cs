using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Text;

namespace Cyborg.Modules.Assert;

[GeneratedModuleValidation]
public sealed partial record AssertModule
(
    [property: Required] ModuleReference Assertion,
    [property: Required][property: IgnoreInterpolation] TaggedString Message
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.assert.v1";
}
