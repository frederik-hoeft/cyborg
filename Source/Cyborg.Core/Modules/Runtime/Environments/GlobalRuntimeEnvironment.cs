using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments;

public sealed class GlobalRuntimeEnvironment(JsonNamingPolicy namingPolicy) : RuntimeEnvironment(name: "global", isTransient: false, namingPolicy);