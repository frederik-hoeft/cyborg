using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Runtime;

namespace Cyborg.TestModules.Cli;

[GeneratedModuleValidation]
public sealed partial record ProbeModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.tests.probe.v1";
}
