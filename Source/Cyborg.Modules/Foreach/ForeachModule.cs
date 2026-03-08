using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;

namespace Cyborg.Modules.Foreach;

public sealed record ForeachModule(string Collection, string ItemVariable, bool ContinueOnError, ModuleContext Body) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.foreach.v1";
}