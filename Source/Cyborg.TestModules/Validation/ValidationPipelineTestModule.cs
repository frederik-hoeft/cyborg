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
    [property: Untagged] string InterpolatedValue,
    [property: Untagged][property: IgnoreInterpolation] string DeferredValue,
    [property: Required(TargetsElements = true)]
    [property: VariableIdentifier(TargetsElements = true)]
    IReadOnlyCollection<string?>? Tags
) : ModuleBase, IModule
{
    [DefaultValue<string>("${deferred_default}")]
    [IgnoreInterpolation]
    [Untagged]
    public string? DeferredDefault { get; init; }

    [Required]
    [Required(TargetsElements = true)]
    public IReadOnlyCollection<string?>? RequiredTags { get; init; } = [];

    public static string ModuleId => "cyborg.test-modules.validation-pipeline.v1";
}
