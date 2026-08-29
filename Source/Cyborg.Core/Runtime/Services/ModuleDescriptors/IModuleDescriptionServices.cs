using Cyborg.Core.Runtime.Services.ModuleDescriptors.Writers;
using Cyborg.Core.Text.Rendering;
using Jab;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

[ServiceProviderModule]
[Singleton<IModuleDescriptionSerializer>(Factory = nameof(CreateTextSerializer))]
[Singleton<IModuleDescriptionSerializer>(Factory = nameof(CreateJsonSerializer))]
[Singleton<IModuleDescriptionSerializerRegistry>(Factory = nameof(CreateSerializerRegistry))]
[Singleton<IModuleSerializationService>(Factory = nameof(CreateSerializationService))]
public interface IModuleDescriptionServices
{
    static IModuleDescriptionSerializer CreateTextSerializer(ITaggedStringRenderer taggedStringRenderer) =>
        new TextModuleDescriptionSerializer(taggedStringRenderer);

    static IModuleDescriptionSerializer CreateJsonSerializer(ITaggedStringRenderer taggedStringRenderer) =>
        new JsonModuleDescriptionSerializer(indented: true, taggedStringRenderer);

    static IModuleDescriptionSerializerRegistry CreateSerializerRegistry(IEnumerable<IModuleDescriptionSerializer> serializers) =>
        new DefaultModuleDescriptionSerializerRegistry(serializers);

    static IModuleSerializationService CreateSerializationService(IModuleDescriptionSerializerRegistry serializerRegistry) => new DefaultModuleSerializationService(serializerRegistry);
}
