using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.TestModules.Description;

[Validatable]
public sealed record DescriptionTestChild
{
    public string Value { get; init; } = string.Empty;
}
