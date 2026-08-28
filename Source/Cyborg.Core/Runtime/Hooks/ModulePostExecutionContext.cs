using Cyborg.Core.Runtime.Engine;

namespace Cyborg.Core.Runtime.Hooks;

internal sealed record ModulePostExecutionContext(IModuleExecutionResult Result, IModuleRuntime Runtime) : IModulePostExecutionContext;
