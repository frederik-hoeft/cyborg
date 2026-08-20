using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Text;
using System.Collections.Immutable;

namespace Cyborg.TestModules.Secrets;

[GeneratedModuleValidation]
public sealed partial record TaggedStringTestModule
(
    TaggedString Plain,
    [property: Secret] TaggedString Secret,
    TaggedString? OptionalSecret,
    [property: Untagged] string IntentionallyUntagged,
    ImmutableArray<TaggedString> Values
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.test-modules.tagged-string.v1";
}
