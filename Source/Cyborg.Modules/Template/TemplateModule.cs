using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Cyborg.Modules.Template;

[GeneratedModuleValidation]
public sealed partial record TemplateModule
(
    [property: Required][property: MatchesRegex(nameof(TemplateModule.NamespaceRegex))] string Namespace,
    [property: Required][property: FileExists] string Path,
    ImmutableArray<DynamicKeyValuePair> Arguments
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.template.v1";

    [GeneratedRegex(@"^[A-Za-z0-9_](\.[A-Za-z0-9_\-]+)*$")]
    private static partial Regex NamespaceRegex { get; }
}