using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Services.Dispatch;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Modules.Borg.Create;

public sealed class BorgCreateModuleWorker
(
    IWorkerContext<BorgCreateModule> context,
    IChildProcessDispatcher processDispatcher,
    IPosixShellCommandBuilder shellCommandBuilder
) : BorgModuleWorker<BorgCreateModule>(context, shellCommandBuilder)
{
    protected async override Task<IModuleExecutionResult> ExecuteAsync([NotNull] IModuleRuntime runtime, CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            "create",
            "--stats", "--json",
            "--compression", Module.Compression
        ];
        if (Module.Exclude.Caches)
        {
            arguments.Add("--exclude-caches");
        }
        foreach (string path in Module.Exclude.Paths)
        {
            arguments.Add("--exclude");
            arguments.Add(path);
        }
        arguments.Add($"{Module.Repository}::{Module.ArchiveName}");
        arguments.Add(Module.SourcePath);
        ProcessStartInfo startInfo = new(Module.Executable, arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        if (Module.RemoteShell is not null)
        {
            startInfo.Environment[BORG_RSH_ENV_VAR] = BuildBorgRsh(Module.RemoteShell);
        }
        startInfo.Environment[BORG_PASSPHRASE_ENV_VAR] = Module.Passphrase;
        ChildProcessResult executionResult = await processDispatcher.ExecuteAsync(startInfo, cancellationToken);
        // TODO: output parsing and metric extraction
        if (executionResult.ExitCode != 0)
        {
            return runtime.Exit(Failed());
        }
        return runtime.Exit(Success());
    }
}