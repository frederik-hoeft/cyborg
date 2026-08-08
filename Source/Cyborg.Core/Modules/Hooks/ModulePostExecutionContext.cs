using Cyborg.Core.Modules.Runtime;

namespace Cyborg.Core.Modules.Hooks;

internal sealed record ModulePostExecutionContext(IModuleExecutionResult Result, IModuleRuntime Runtime, IModuleResultBuilder ResultBuilder) : IModulePostExecutionContext;
