using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using System.Collections.Immutable;

namespace Cyborg.Modules.Borg;

public sealed record BorgBackupModule(ImmutableArray<BorgRemote> Remotes) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.backup.v1";
}

public sealed record BorgRemote(string Hostname, int Port, string? WakeOnLanMac, string BorgRsh, string BorgRepoRoot);

public sealed record BorgJobModule
(
    ModuleContext Job,
    ModuleContext? BeforeJob,
    ModuleContext? AfterJob,
    ModuleContext? OnError
) : IModule
{
    public static string ModuleId => "cyborg.modules.borg.job.v1";
}

public sealed class JobModuleWorker(BorgJobModule module) : ModuleWorker<BorgJobModule>(module)
{
    protected async override Task<bool> ExecuteAsync(IModuleRuntime runtime, CancellationToken cancellationToken)
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