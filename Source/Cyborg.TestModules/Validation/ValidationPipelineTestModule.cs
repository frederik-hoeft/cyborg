using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using System.Collections.Immutable;

namespace Cyborg.TestModules.Validation;

[GeneratedModuleValidation]
public sealed partial record ValidationPipelineTestModule
(
    [property: Required] ImmutableArray<ValidationPipelineTestItem> RequiredItems,
    ImmutableArray<ValidationPipelineTestItem> OptionalItems,
    ImmutableArray<ValidationPipelineTestItem>? NullableItems,
    string InterpolatedValue,
    [property: IgnoreInterpolation] string DeferredValue
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.test-modules.validation-pipeline.v1";
}
