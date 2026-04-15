using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Text.Json;

namespace Cyborg.Core.Modules.Runtime.Environments;

public sealed record GlobalRuntimeEnvironment(JsonNamingPolicy NamingPolicy) : RuntimeEnvironment(Name: "global", IsTransient: false, new VariableSyntaxBuilder(NamingPolicy), Namespace: string.Empty);