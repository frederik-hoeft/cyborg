using Cyborg.Core.Modules;
using System.Collections.Immutable;

namespace Cyborg.Modules.Subprocess;

public sealed record SubprocessModule(string Executable, ImmutableArray<string> Arguments) : IModule
{
    public static string ModuleId => "cyborg.modules.subprocess.v1";
}
