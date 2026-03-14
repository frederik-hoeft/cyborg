using ConsoleAppFramework;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Modules.Switch;
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
        defaultEnvironment.SetVariable("template", template);
        IModuleConfigurationLoader configurationLoader = sp.GetService<IModuleConfigurationLoader>();
        ModuleContext module = await configurationLoader.LoadModuleAsync("test.json", cancellationToken);
        module = module with 
        {
            Environment = module.Environment ?? ModuleEnvironment.Default,
        };
        IModuleRuntime runtime = sp.GetRequiredService<IModuleRuntime>();
        await runtime.ExecuteAsync(module, cancellationToken);
    }
}
