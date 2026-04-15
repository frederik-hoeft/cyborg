using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Configuration.Model;

namespace Cyborg.Modules.Network.SshShutdown;

[GeneratedDecomposition]
public sealed partial record SshShutdownModuleResult(int ExitCode, string? StandardOutput, string? StandardError) : IDecomposable;