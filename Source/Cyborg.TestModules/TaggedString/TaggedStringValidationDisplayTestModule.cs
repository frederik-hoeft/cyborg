using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Text;

namespace Cyborg.TestModules.Secrets;

[GeneratedModuleValidation]
public sealed partial record TaggedStringValidationDisplayTestModule
(
    [property: VariableIdentifier] TaggedString Value
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.test-modules.tagged-string-validation-display.v1";
}
