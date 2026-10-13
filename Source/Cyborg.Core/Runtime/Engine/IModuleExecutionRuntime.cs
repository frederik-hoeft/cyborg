﻿using Cyborg.Core.Runtime.Configuration;
using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Model;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Exposes invocation metadata and test-adapter worker execution without providing
/// a callback surface for current-invocation module dispatch.
/// </summary>
internal interface IModuleExecutionRuntime : IModuleRuntime
{
    ModuleInvocationContext? InvocationContext { get; }

    IServiceProvider? ExecutionServices { get; }

    Task<IModuleExecutionResult> ExecuteActivatedWorkerAsync(IModuleWorker module, IRuntimeEnvironment environment, CancellationToken cancellationToken);
}
