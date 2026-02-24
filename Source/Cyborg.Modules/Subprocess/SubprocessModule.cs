using Cyborg.Core.Modules;
using System.Collections.Immutable;

namespace Cyborg.Modules.Subprocess;

public sealed record SubprocessModule(SubprocessCommand Command, SubprocessOutputOptions? Output) : IModule
{
    public static string ModuleId => "cyborg.modules.subprocess.v1";

    public static string StandardOutputName => "stdout";

    public static string StandardErrorName => "stderr";
}

public sealed record SubprocessCommand(string Executable, ImmutableArray<string> Arguments);

public sealed record SubprocessOutputOptions
(
    string? Namespace = null,
    bool ReadStdout = false,
    bool ReadStderr = false
);