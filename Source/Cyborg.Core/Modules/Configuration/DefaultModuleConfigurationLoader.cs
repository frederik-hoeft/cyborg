using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Services.Security.Trust;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleConfigurationLoader
(
    IJsonLoaderContext configurationContext,
    IConfigurationTrustMonitor trustMonitor,
    IConfigurationTrustService trustService
) : IModuleConfigurationLoader
{
    public async Task<ModuleContext> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken)
    {
        ConfigurationTrustDecision trustDecision = await trustMonitor.EvaluateAsync(configurationFilePath, cancellationToken);
        trustService.Enforce(trustDecision);
        await using FileStream stream = File.OpenRead(trustDecision.Path);
        ModuleConfigurationDeserializer deserializer = new(configurationContext);
        ModuleContext? moduleContext = await deserializer.DeserializeAsync(stream, cancellationToken);
        return moduleContext ?? throw new InvalidOperationException($"Failed to load module context from configuration file '{trustDecision.Path}'.");
    }
}
