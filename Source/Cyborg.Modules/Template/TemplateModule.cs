using Cyborg.Core.Modules;
using System.Collections.Immutable;

namespace Cyborg.Modules.Template;

public sealed record TemplateModule(ImmutableArray<TemplateReference> Templates) : IModule
{
    public static string LoadTargetName => "template";

    public static string ModuleId => "cyborg.modules.template.v1";
}

public sealed record TemplateReference(string Name, string Path);