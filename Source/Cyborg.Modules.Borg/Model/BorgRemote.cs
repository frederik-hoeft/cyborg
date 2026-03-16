using Cyborg.Core.Aot.Modules.Composition;

namespace Cyborg.Modules.Borg;

[GeneratedDecomposition]
public sealed partial record BorgRemote(string Hostname, int Port, string? WakeOnLanMac, string BorgRsh, string BorgRepoRoot, string BorgUser);
