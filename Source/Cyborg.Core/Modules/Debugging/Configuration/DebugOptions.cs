using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.Core.Modules.Debugging.Configuration;

[GeneratedDecomposition]
public sealed partial record DebugOptions(string? Frontend)
{
    public static DebugOptions Default { get; } = new(Frontend: null);
}
