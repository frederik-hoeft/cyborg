using Cyborg.Core.Modules.Descriptors.Writers;
using Jab;

namespace Cyborg.Core.Modules.Descriptors;

[ServiceProviderModule]
[Singleton<IModuleDescriptionSerializer>(Factory = nameof(CreateTextSerializer))]
[Singleton<IModuleDescriptionSerializer>(Factory = nameof(CreateJsonSerializer))]
[Singleton<IModuleDescriptionSerializerRegistry>(Factory = nameof(CreateSerializerRegistry))]
[Singleton<IModuleSerializationService>(Factory = nameof(CreateSerializationService))]
public interface IModuleDescriptionServices
{
    static IModuleDescriptionSerializer CreateTextSerializer() => TextModuleDescriptionSerializer.Instance;

    static IModuleDescriptionSerializer CreateJsonSerializer() => new JsonModuleDescriptionSerializer(indented: true);

    static IModuleDescriptionSerializerRegistry CreateSerializerRegistry(IEnumerable<IModuleDescriptionSerializer> serializers) =>
        new DefaultModuleDescriptionSerializerRegistry(serializers);

    static IModuleSerializationService CreateSerializationService(IModuleDescriptionSerializerRegistry serializerRegistry) => new DefaultModuleSerializationService(serializerRegistry);
}
