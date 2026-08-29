using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Engine.Transactions;

internal readonly record struct EnvironmentVariableBinding(RuntimeEnvironmentId EnvironmentId, string Name);
