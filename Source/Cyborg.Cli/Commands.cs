using ConsoleAppFramework;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Modules.Template;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must be instance method for ConsoleAppFramework.")]
internal sealed class Commands
{
    [Command("run")]
    public async Task RunAsync([Argument] string template, CancellationToken cancellationToken = default)
    {
        using DefaultServiceProvider sp = new();
        DefaultEnvironment defaultEnvironment = sp.GetRequiredService<DefaultEnvironment>();
        defaultEnvironment.SetVariable(TemplateModule.LoadTargetName, template);
        IModuleConfigurationLoader configurationLoader = sp.GetService<IModuleConfigurationLoader>();
        IModuleWorker module = await configurationLoader.LoadModuleAsync("config.json", cancellationToken);
        await module.ExecuteAsync(cancellationToken);
    }
}
