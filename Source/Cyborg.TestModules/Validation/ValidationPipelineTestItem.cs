using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.TestModules.Validation;

[Validatable]
public sealed record ValidationPipelineTestItem
(
    [property: Required]
    [property: DefaultValue<string>("${fallback}")]
    string Value
);
