namespace Cyborg.Core.Configuration.Serialization.Dynamics;

public interface IDynamicValueProviderRegistry
{
    bool TryGetProvider(string typeName, [NotNullWhen(true)] out IDynamicValueProvider? provider);

    bool TryGetGenericProviderFactory(string typeName, int arity, [NotNullWhen(true)] out IDynamicGenericValueProviderFactory? providerFactory);

    bool TryResolveProvider(DynamicValueTypeReference typeReference, [NotNullWhen(true)] out IDynamicValueProvider? provider);

    IEnumerable<string> GetRegisteredTypeNames();
}