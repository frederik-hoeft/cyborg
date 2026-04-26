using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Modules.Subprocess;

[GeneratedModuleValidation]
public sealed partial record SubprocessModule
(
    [property: Required] SubprocessCommand Command,
    [property: Required][property: DefaultInstance] SubprocessOutputOptions Output,
    [property: DefaultValue<bool>(true)] bool CheckExitCode,
    ImpersonationContext? Impersonation,
    IReadOnlyCollection<EnvironmentVariable>? EnvironmentVariables
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.subprocess.v1";
}

[Validatable]
public sealed record ImpersonationContext
(
    [property: Required][property: DefaultValue<string>("/usr/sbin/runuser")][property: FileExists] string Executable,
    [property: Required] string User
);

[Validatable]
public sealed record SubprocessCommand
(
    [property: Required][property: FileExists] string Executable,
    [property: Required] IReadOnlyCollection<string> Arguments
);

[Validatable]
public sealed record SubprocessOutputOptions
(
    bool ReadStdout,
    bool ReadStderr
) : IDefaultInstance<SubprocessOutputOptions>
{
    public static SubprocessOutputOptions Default => new(ReadStdout: false, ReadStderr: false);
}

[Validatable]
[GeneratedDecomposition]
public sealed partial record EnvironmentVariable
(
    [property: Required] string Key,
    [property: Required] string Value
) : IDecomposable;
