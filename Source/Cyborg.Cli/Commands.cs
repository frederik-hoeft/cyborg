using ConsoleAppFramework;
using Cyborg.Cli.Logging;
using Cyborg.Core.Modules.Configuration;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime;
using Cyborg.Core.Modules.Runtime.Environments;
using Cyborg.Core.Services.Metrics;
using Cyborg.Modules.Borg;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Must be instance method for ConsoleAppFramework.")]
internal sealed class Commands
{
    [Command("run")]
    public async Task RunAsync([Argument] string template, string metricsNamespace = "cyborg", bool dryRun = false, LogLevel logLevel = LogLevel.Information, CancellationToken cancellationToken = default)
    {
        using DefaultServiceProvider sp = new();
        sp.GetRequiredService<LoggingOptions>().MinimumLevel = logLevel;
        ILogger<Commands> logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<Commands>();
        GlobalRuntimeEnvironment globalEnvironment = sp.GetRequiredService<GlobalRuntimeEnvironment>();
        globalEnvironment.SetVariable("template", template);
        if (dryRun)
        {
            globalEnvironment.SetVariable(BorgWellKnownVariables.DRY_RUN, true);
        }
        sp.GetRequiredService<MetricsCollectorOptions>().Namespace = metricsNamespace;
        IModuleConfigurationLoader configurationLoader = sp.GetService<IModuleConfigurationLoader>();
        ModuleContext module = await configurationLoader.LoadModuleAsync("test.json", cancellationToken);
        module = module with 
        {
            Environment = module.Environment ?? ModuleEnvironment.Default,
        };
        IModuleRuntime runtime = sp.GetRequiredService<IModuleRuntime>();
        logger.LogRunStarted(template);
        IModuleExecutionResult result = await runtime.ExecuteAsync(module, cancellationToken);
        if (result.Status is ModuleExitStatus.Success or ModuleExitStatus.Skipped)
        {
            logger.LogRunCompleted(template);
        }
        else
        {
            logger.LogRunCompletedWithStatus(template, result.Status.ToString());
        }
    }
}
