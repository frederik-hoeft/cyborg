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
    public static IModuleDescriptionSerializer CreateTextSerializer() => TextModuleDescriptionSerializer.Instance;

    public static IModuleDescriptionSerializer CreateJsonSerializer() => new JsonModuleDescriptionSerializer(indented: true);

    public static IModuleDescriptionSerializerRegistry CreateSerializerRegistry(IEnumerable<IModuleDescriptionSerializer> serializers) =>
        new DefaultModuleDescriptionSerializerRegistry(serializers);

    public static IModuleSerializationService CreateSerializationService(IModuleDescriptionSerializerRegistry serializerRegistry) => new DefaultModuleSerializationService(serializerRegistry);
}
