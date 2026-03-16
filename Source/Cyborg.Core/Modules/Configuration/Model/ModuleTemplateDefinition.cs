using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Core.Modules.Configuration.Model;

[Validatable]
public sealed record ModuleTemplateDefinition
(
    string? Namespace,
    IReadOnlyCollection<string> Arguments
) : IDefaultInstance<ModuleTemplateDefinition>
{
    public static ModuleTemplateDefinition Default => new(Namespace: null, Arguments: []);
}