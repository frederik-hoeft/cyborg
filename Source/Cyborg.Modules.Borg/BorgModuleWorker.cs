using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Validation;
using Cyborg.Core.Services.Dispatch;
using Cyborg.Modules.Borg.Model;

namespace Cyborg.Modules.Borg;

public abstract class BorgModuleWorker<TModule>
(
    IWorkerContext<TModule> context,
    IPosixShellCommandBuilder shellCommandBuilder
) : ModuleWorker<TModule>(context) where TModule : BorgModuleBase, IModule<TModule>
{
    protected const string BORG_RSH_ENV_VAR = "BORG_RSH";
    protected const string BORG_PASSPHRASE_ENV_VAR = "BORG_PASSPHRASE";

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
