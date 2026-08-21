using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Configuration.Model;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Text;

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
    [property: Required][property: Untagged][property: DefaultValue<string>("/usr/sbin/runuser")][property: FileExists] string Executable,
    [property: Required][property: Untagged] string User
);

[Validatable]
public sealed record SubprocessCommand
(
    [property: Required][property: Untagged][property: FileExists] string Executable,
    [property: Required] IReadOnlyCollection<TaggedString> Arguments,
    [property: Untagged][property: RootedPath][property: NormalizedPath][property: DirectoryExists] string? WorkingDirectory
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
    [property: Required][property: Untagged] string Key,
    [property: Required] TaggedString Value
) : IDecomposable;
