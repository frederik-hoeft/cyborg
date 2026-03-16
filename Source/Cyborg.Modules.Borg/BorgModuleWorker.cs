using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Modules.Borg.Model;
using System.Diagnostics;

namespace Cyborg.Modules.Borg;

public abstract class BorgModuleWorker<TModule>
(
    IWorkerContext<TModule> context,
    IPosixShellCommandBuilder shellCommandBuilder
) : ModuleWorker<TModule>(context) where TModule : BorgModuleBase, IModule<TModule>
{
    protected const string BORG_RSH_ENV_VAR = "BORG_RSH";
    protected const string BORG_PASSPHRASE_ENV_VAR = "BORG_PASSPHRASE";

    protected void AddDefaults(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        if (Module.RemoteShell is not null)
        {
            startInfo.Environment[BORG_RSH_ENV_VAR] = BuildBorgRsh(Module.RemoteShell);
        }
        startInfo.Environment[BORG_PASSPHRASE_ENV_VAR] = Module.Passphrase;
    }

    protected string BuildBorgRsh(BorgSshOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> arguments = [];

        if (options.SshPass is { } sshPass)
        {
            arguments.Add(sshPass.Executable);
            arguments.Add($"-f{sshPass.FilePath}");

            if (!string.IsNullOrWhiteSpace(sshPass.MatchPrompt))
            {
                arguments.Add("-P");
                arguments.Add(sshPass.MatchPrompt);
            }
        }

        arguments.Add(options.Executable);

        return shellCommandBuilder.BuildCommand(arguments);
    }
}
