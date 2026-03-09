using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Conditional;

public sealed record IfModule(ModuleReference Condition, ModuleContext Then, ModuleContext? Else = null) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.if.v1";
}