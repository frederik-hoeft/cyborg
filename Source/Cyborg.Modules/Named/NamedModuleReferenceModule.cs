using Cyborg.Core.Modules;

namespace Cyborg.Modules.Named;

public sealed record NamedModuleReferenceModule(string Target) : IModule
{
    public static string ModuleId => "cyborg.modules.named.ref.v1";
}