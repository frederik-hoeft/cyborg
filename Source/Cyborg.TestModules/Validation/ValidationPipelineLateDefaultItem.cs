using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.TestModules.Validation;

[Validatable]
public sealed record ValidationPipelineLateDefaultItem
(
    [property: IgnoreOverride]
    [property: DefaultValue<string>("&{source}")]
    [property: Untagged]
    string Value,
    [property: IgnoreOverride]
    [property: DefaultValue<string>("&{#source}")]
    [property: Untagged]
    string Escaped
);
