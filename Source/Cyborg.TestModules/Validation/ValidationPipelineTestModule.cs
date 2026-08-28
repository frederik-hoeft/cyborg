using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime;
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

    [MinLength(1)]
    public ImmutableArray<string> LengthCheckedItems { get; init; } = ["value"];

    [MinLength(1)]
    public ImmutableArray<string>? NullableLengthCheckedItems { get; init; }

    [Required]
    public ImmutableArray<string>? RequiredNullableImmutableItems { get; init; } = [];

    [ExactLength(1)]
    public string[] ArrayLengthCheckedItems { get; init; } = ["value"];

    public ValidationPipelineTestItem ReferenceItem { get; init; } = new("literal");

    public ValidationPipelineTestItem? NullableReferenceItem { get; init; }

    public ValidationPipelineValueItem ValueItem { get; init; } = new("literal");

    public ValidationPipelineValueItem? NullableValueItem { get; init; }

    public ImmutableArray<ValidationPipelineTestItem?> NullableElementItems { get; init; } = [];

    public ImmutableArray<ValidationPipelineValueItem?> NullableValueElementItems { get; init; } = [];

    public ValidationPipelineStructCollection<ValidationPipelineTestItem> StructCollectionItems { get; init; } = [];

    public ImmutableArray<ValidationPathTestItem> ValidationPathItems { get; init; } = [];

    public ImmutableArray<ValidationPathContainerItem> RecursiveValidationPathItems { get; init; } = [];

    [MinLength(1, TargetsElements = true)]
    public IReadOnlyCollection<ImmutableArray<string>?> NestedLengthItems { get; init; } = [];

    public static string ModuleId => "cyborg.test-modules.validation-pipeline.v1";
}
