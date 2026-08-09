using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using System.Collections.Immutable;

namespace Cyborg.TestModules.Description;

[GeneratedModuleValidation]
public sealed partial record GeneratedDescriptionTestModule : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.tests.generated-description.v1";

    public string Text { get; init; } = string.Empty;

    public string? OptionalText { get; init; }

    public char Marker { get; init; }

    public string[] ArrayValues { get; init; } = [];

    public IReadOnlyCollection<string>? OptionalValues { get; init; }

    public DescriptionTestChild? Child { get; init; }

    public IReadOnlyCollection<DescriptionTestChild> Children { get; init; } = [];

    public IReadOnlyCollection<DescriptionTestChild?> NullableChildren { get; init; } = [];

    public ImmutableArray<string>? OptionalImmutableValues { get; init; }

    public ImmutableArray<string> Values { get; init; }
}
