using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Modules.Runtime.Transactions;

internal readonly record struct EnvironmentVariableBinding(RuntimeEnvironmentId EnvironmentId, string Name);
