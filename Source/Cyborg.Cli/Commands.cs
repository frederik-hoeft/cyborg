using ConsoleAppFramework;
using Cyborg.Core.Modules;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environements;
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
        GlobalRuntimeEnvironment defaultEnvironment = sp.GetRequiredService<GlobalRuntimeEnvironment>();
        defaultEnvironment.SetVariable(TemplateModule.LoadTargetName, template);
        IModuleConfigurationLoader configurationLoader = sp.GetService<IModuleConfigurationLoader>();
        IModuleWorker module = await configurationLoader.LoadModuleAsync("config.json", cancellationToken);
        IModuleRuntime runtime = sp.GetRequiredService<IModuleRuntime>();
        await runtime.ExecuteAsync(module, defaultEnvironment, cancellationToken);
    }
}
