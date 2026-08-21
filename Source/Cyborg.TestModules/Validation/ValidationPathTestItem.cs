using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.TestModules.Validation;

[Validatable]
public sealed record ValidationPathTestItem
(
    [property: Required]
    [property: Untagged]
    string? Value,
    [property: Required(TargetsElements = true)]
    IReadOnlyCollection<string?> Values
);
