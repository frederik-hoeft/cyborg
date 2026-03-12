using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Validation;
using System.Collections.Immutable;

namespace Cyborg.Modules.Subprocess;

[GeneratedModuleValidation]
public sealed partial record SubprocessModule
(
    [property: Required] SubprocessCommand Command,
    [property: Required][property: DefaultInstance] SubprocessOutputOptions Output,
    [property: DefaultValue<bool>(true)] bool CheckExitCode
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.subprocess.v1";
}

[Validatable]
public sealed record SubprocessCommand
(
    [property: Required][property: MinLength(1)] string Executable,
    [property: Required] ImmutableArray<string> Arguments
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