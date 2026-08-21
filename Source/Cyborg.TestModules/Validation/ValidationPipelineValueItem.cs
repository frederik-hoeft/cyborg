using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.TestModules.Validation;

[Validatable]
public readonly record struct ValidationPipelineValueItem
(
    [property: Required]
    [property: Untagged]
    [property: DefaultValue<string>("${fallback}")]
    string Value
);
