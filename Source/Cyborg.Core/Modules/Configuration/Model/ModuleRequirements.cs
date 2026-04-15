using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ModuleRequirements
(
    string? ArgumentNamespace,
    IReadOnlyCollection<string> Arguments
) : IDefaultInstance<ModuleRequirements>
{
    public static ModuleRequirements Default => new(ArgumentNamespace: null, Arguments: []);
}