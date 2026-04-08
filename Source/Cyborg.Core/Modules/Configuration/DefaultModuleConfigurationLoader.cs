using Cyborg.Core.Configuration.Serialization;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Configuration.Serialization;
using System.Text.Json;

namespace Cyborg.Core.Modules.Configuration;

public sealed class DefaultModuleConfigurationLoader(IJsonLoaderContext configurationContext) : IModuleConfigurationLoader
{
    public async Task<ModuleContext> LoadModuleAsync(string configurationFilePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(configurationFilePath);
        ModuleContext? moduleReference = await JsonSerializer.DeserializeAsync<ModuleContext>(stream, configurationContext, cancellationToken);
        return moduleReference ?? throw new InvalidOperationException($"Failed to load module context from configuration file '{configurationFilePath}'.");
    }
}