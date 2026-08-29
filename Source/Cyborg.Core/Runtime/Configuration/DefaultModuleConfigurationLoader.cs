using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Services.Security.Trust;

namespace Cyborg.Core.Runtime.Configuration;

public sealed class DefaultModuleConfigurationLoader
(
    IJsonLoaderContext configurationContext,
    IConfigurationTrustMonitor trustMonitor,
    IConfigurationTrustService trustService
) : IModuleConfigurationLoader
{
    public async Task<ModuleConfigurationLoadResult> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken)
    {
        ConfigurationTrustDecision trustDecision = await trustMonitor.EvaluateAsync(configurationFilePath, cancellationToken);
        trustService.Enforce(trustDecision);
        await using FileStream stream = File.OpenRead(trustDecision.Path);
        ModuleConfigurationDeserializer deserializer = new(configurationContext);
        ModuleConfigurationLoadResult? configuration = await deserializer.DeserializeAsync(stream, cancellationToken);
        return configuration ?? throw new InvalidOperationException($"Failed to load module configuration from configuration file '{trustDecision.Path}'.");
    }
}
