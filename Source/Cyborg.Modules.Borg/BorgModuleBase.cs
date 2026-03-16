using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Modules.Borg.Model;

namespace Cyborg.Modules.Borg;

public abstract record BorgModuleBase : ModuleBase
{
    [Required]
    [FileExists]
    [DefaultValue<string>("/usr/bin/borg")]
    public string Executable { get; init; } = null!;

    [Required]
    public string Repository { get; init; } = null!;

    [Required]
    public string Passphrase { get; init; } = null!;

    public BorgSshOptions? RemoteShell { get; init; }
}
