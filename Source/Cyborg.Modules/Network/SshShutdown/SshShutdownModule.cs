using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules;

namespace Cyborg.Modules.Network.SshShutdown;

[GeneratedModuleValidation]
public sealed partial record SshShutdownModule
(
    [property: Required][property: Untagged][property: DefaultValue<string>("/usr/bin/ssh")][property: FileExists] string Executable,
    [property: Required][property: Untagged] string Hostname,
    [property: Required][property: Untagged] string Username,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)][property: DefaultValue<int>(22)] int Port,
    [property: Required][property: Untagged][property: DefaultValue<string>("/usr/bin/shutdown -h now")] string ShutdownCommand,
    SshPass? SshPass
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.network.ssh_shutdown.v1";
}

[Validatable]
public sealed record SshPass
(
    [property: Required][property: Untagged][property: DefaultValue<string>("/usr/bin/sshpass")][property: FileExists] string Executable,
    [property: Required][property: Untagged][property: FileExists] string FilePath,
    [property: Untagged] string? MatchPrompt
);
