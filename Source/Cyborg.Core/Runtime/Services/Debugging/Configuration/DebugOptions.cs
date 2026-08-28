using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.Core.Runtime.Services.Debugging.Configuration;

[GeneratedDecomposition]
public sealed partial record DebugOptions(string? Frontend)
{
    public static DebugOptions Default { get; } = new(Frontend: null);
}
