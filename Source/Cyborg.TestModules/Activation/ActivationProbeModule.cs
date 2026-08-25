using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;

namespace Cyborg.TestModules.Activation;

[GeneratedModuleValidation]
public sealed partial record ActivationProbeModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.test-modules.activation-probe.v1";
}
