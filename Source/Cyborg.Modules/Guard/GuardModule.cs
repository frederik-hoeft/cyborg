using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Guard;

[GeneratedModuleValidation]
public sealed partial record GuardModule
(
    [property: Required] ModuleContext Try,
    ModuleContext? Catch,
    [property: Required] ModuleContext Finally,
    [property: DefinedEnumValue][property: DefaultValue<GuardModuleBehavior>(GuardModuleBehavior.Rethrow)] GuardModuleBehavior Behavior
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.guard.v1";
}

public enum GuardModuleBehavior
{
    Rethrow,
    Swallow,
}