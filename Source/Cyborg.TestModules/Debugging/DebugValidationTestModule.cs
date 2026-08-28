using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Runtime;

namespace Cyborg.TestModules.Debugging;

[GeneratedModuleValidation]
public sealed partial record DebugValidationTestModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.test-modules.debug-validation.v1";
}
