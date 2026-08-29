namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

public interface IModuleDescriptionSerializerRegistry
{
    IModuleDescriptionSerializer GetRequiredSerializer(string format);

    bool TryGetSerializer(string format, [NotNullWhen(true)] out IModuleDescriptionSerializer? serializer);
}
