using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Modules.Borg.Model;

[Validatable]
public sealed record BorgSshOptions
(
    [property: Required][property: DefaultValue<string>("/usr/bin/ssh")][property: FileExists] string Executable,
    BorgSshPass? SshPass
);