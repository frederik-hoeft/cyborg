using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;
using Cyborg.Core.Text;
using Cyborg.Modules.Borg.Model;

namespace Cyborg.Modules.Borg;

public abstract record BorgModuleBase : ModuleBase
{
    [Required]
    [Untagged]
    [FileExists]
    [DefaultValue<string>("/usr/bin/borg")]
    public string Executable { get; init; } = null!;

    [Required]
    [Secret]
    public TaggedString Passphrase { get; init; }

    public BorgSshOptions? RemoteShell { get; init; }

    [Required]
    public BorgRemoteRepository RemoteRepository { get; init; } = null!;
}
