namespace Cyborg.Core.Modules.Descriptors;

public interface IModuleDescriptionSerializerRegistry
{
    IModuleDescriptionSerializer GetRequiredSerializer(string format);

    bool TryGetSerializer(string format, [NotNullWhen(true)] out IModuleDescriptionSerializer? serializer);
}
