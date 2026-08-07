using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Descriptors;

public interface IModuleDescriptionSerializerRegistry
{
    IModuleDescriptionSerializer GetRequired(string format);

    bool TryGet(
        string format,
        [NotNullWhen(true)] out IModuleDescriptionSerializer? serializer);
}
