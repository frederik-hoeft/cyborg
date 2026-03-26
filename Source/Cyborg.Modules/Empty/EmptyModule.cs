using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Empty;

[GeneratedModuleValidation]
public sealed partial record EmptyModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.empty.v1";
}