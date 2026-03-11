using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using Cyborg.Core.Modules.Runtime;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cyborg.Modules.Borg;

public sealed record BorgBackupModule(ImmutableArray<BorgRemote> Remotes) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.backup.v1";
}

[GeneratedDecomposition]
public sealed partial record BorgRemote(string Hostname, int Port, string? WakeOnLanMac, string BorgRsh, string BorgRepoRoot);

public sealed class BorgRemoteValueProvider : IDynamicValueProvider
{
    public string TypeName => "cyborg.types.borg.remote.v1";

    public bool TryCreateValue(ref Utf8JsonReader reader, IModuleLoaderContext context, [NotNullWhen(true)] out DynamicValue? value)
    {
        BorgRemote? remote = JsonSerializer.Deserialize<BorgRemote>(ref reader, context);
        if (remote is null)
        {
            value = null;
            return false;
        }
        value = new DynamicValue(remote);
        return true;
    }
}

[GeneratedModuleValidation]
public sealed partial record BorgJobModule
(
    ModuleContext Job,
    ModuleContext? BeforeJob,
    ModuleContext? AfterJob,
    ModuleContext? OnError
) : ModuleBase, IModule
{
    public static string ModuleId => "cyborg.modules.borg.job.v1";
}

public sealed class JobModuleWorker(IWorkerContext<BorgJobModule> context) : ModuleWorker<BorgJobModule>(context)
{
    protected async override Task<bool> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        try
        {
            if (Module.BeforeJob is not null)
            {
                bool beforeJobResult = await runtime.ExecuteAsync(Module.BeforeJob, cancellationToken);
                if (!beforeJobResult)
                {
                    return false;
                }
            }
            bool jobResult = await runtime.ExecuteAsync(Module.Job, cancellationToken);
            if (!jobResult)
            {
                return false;
            }
            if (Module.AfterJob is not null)
            {
                bool afterJobResult = await runtime.ExecuteAsync(Module.AfterJob, cancellationToken);
                if (!afterJobResult)
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception)
        {
            if (Module.OnError is not null)
            {
                return await runtime.ExecuteAsync(Module.OnError, cancellationToken);
            }
            else
            {
                throw;
            }
        }
    }
}