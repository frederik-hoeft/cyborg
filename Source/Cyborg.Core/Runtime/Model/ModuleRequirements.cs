using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Model;

[Validatable]
public sealed record ModuleRequirements
(
    [property: Untagged] string? ArgumentNamespace,
    IReadOnlyCollection<string> Arguments
) : IDefaultInstance<ModuleRequirements>
{
    public static ModuleRequirements Default => new(ArgumentNamespace: null, Arguments: []);
}
