using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Assert;

[GeneratedModuleValidation]
public sealed partial record AssertModule
(
    [property: Required] ModuleReference Assertion,
    [property: Required] string Message
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.assert.v1";
}