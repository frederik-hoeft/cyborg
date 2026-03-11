using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;

namespace Cyborg.Modules.Template;

[GeneratedModuleValidation]
public sealed partial record TemplateModule([property: MinLength(1)] ImmutableArray<TemplateReference> Templates) : ModuleBase, IModule
{
    public static string LoadTargetName => "template";

    public static string ModuleId => "cyborg.modules.template.v1";
}

[Validatable]
public sealed record TemplateReference
(
    [property: Required][property: MinLength(1)] string Name,
    [property: Required][property: MinLength(1)] string Path
);