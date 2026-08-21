using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Modules.Borg.Model;

[Validatable]
[GeneratedDecomposition]
public sealed partial record BorgSshPass
(
    [property: Required][property: Untagged][property: DefaultValue<string>("/usr/bin/sshpass")][property: FileExists] string Executable,
    [property: Required][property: Untagged][property: FileExists] string FilePath,
    [property: Untagged] string? MatchPrompt
);
