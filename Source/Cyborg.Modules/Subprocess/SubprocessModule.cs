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
    [property: Required][property: DefaultInstance] SubprocessOutputOptions Output
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
    [property: Required][property: DefaultValue<string>(SubprocessOutputOptions.STDOUT_VARIABLE_NAME_DEFAULT)] string StdoutVariableName,
    [property: Required][property: DefaultValue<string>(SubprocessOutputOptions.STDERR_VARIABLE_NAME_DEFAULT)] string StderrVariableName,
    bool ReadStdout,
    bool ReadStderr
) : IDefaultInstance<SubprocessOutputOptions>
{
    public static SubprocessOutputOptions Default => new(STDOUT_VARIABLE_NAME_DEFAULT, STDERR_VARIABLE_NAME_DEFAULT, ReadStdout: false, ReadStderr: false);

    private const string STDOUT_VARIABLE_NAME_DEFAULT = "cyborg.modules.subprocess.v1.stdout";

    private const string STDERR_VARIABLE_NAME_DEFAULT = "cyborg.modules.subprocess.v1.stderr";
}