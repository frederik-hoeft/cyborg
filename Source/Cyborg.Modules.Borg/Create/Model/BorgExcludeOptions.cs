using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Modules.Borg.Create.Model;

[Validatable]
[GeneratedDecomposition]
public sealed partial record BorgExcludeOptions
(
    bool Caches,
    [property: Required] IReadOnlyCollection<string> Paths
) : IDefaultInstance<BorgExcludeOptions>
{
    public static BorgExcludeOptions Default { get; } = new(Caches: false, Paths: []);
}
