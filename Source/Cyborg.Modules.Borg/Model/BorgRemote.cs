using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Modules.Borg.Model;

namespace Cyborg.Modules.Borg;

[GeneratedDecomposition]
public sealed partial record BorgRemote
(
    string Hostname,
    int Port,
    string? WakeOnLanMac,
    string BorgRepoRoot,
    string BorgUser,
    BorgSshOptions RemoteShell
);
