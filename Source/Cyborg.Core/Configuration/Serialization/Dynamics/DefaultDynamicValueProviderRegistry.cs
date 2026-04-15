using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Cyborg.Core.Configuration.Serialization.Dynamics;

public sealed class DynamicValueProviderRegistry(IEnumerable<IDynamicValueProvider> providers, IEnumerable<IDynamicGenericValueProviderFactory> genericProviderFactories) : IDynamicValueProviderRegistry
{
    private readonly FrozenDictionary<string, IDynamicValueProvider> _providers = providers
        .ToFrozenDictionary(static provider => provider.TypeName, StringComparer.Ordinal);

    private readonly FrozenDictionary<(string TypeName, int Arity), IDynamicGenericValueProviderFactory> _genericProviderFactories = genericProviderFactories
        .ToFrozenDictionary(static factory => (factory.TypeName, factory.Arity));

    public bool TryGetProvider(string typeName, [NotNullWhen(true)] out IDynamicValueProvider? provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return _providers.TryGetValue(typeName, out provider);
    }

    public bool TryGetGenericProviderFactory(string typeName, int arity, [NotNullWhen(true)] out IDynamicGenericValueProviderFactory? providerFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentOutOfRangeException.ThrowIfLessThan(arity, 1);

        return _genericProviderFactories.TryGetValue((typeName, arity), out providerFactory);
    }

    public IEnumerable<string> GetRegisteredTypeNames() => _providers.Keys.Concat(_genericProviderFactories.Keys.Select(static key => $"{key.TypeName}`{key.Arity}"));

    public bool TryResolveProvider(DynamicValueTypeReference typeReference, [NotNullWhen(true)] out IDynamicValueProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(typeReference);
        if (!typeReference.IsGeneric)
        {
            return TryGetProvider(typeReference.TypeName, out provider);
        }

        if (!TryGetGenericProviderFactory(typeReference.TypeName, typeReference.TypeArguments.Length, out IDynamicGenericValueProviderFactory? providerFactory))
        {
            provider = null;
            return false;
        }

        ImmutableArray<IDynamicValueProvider>.Builder typeArguments =
            ImmutableArray.CreateBuilder<IDynamicValueProvider>(typeReference.TypeArguments.Length);

        foreach (DynamicValueTypeReference typeArgument in typeReference.TypeArguments)
        {
            if (!TryResolveProvider(typeArgument, out IDynamicValueProvider? typeArgumentProvider))
            {
                provider = null;
                return false;
            }

            typeArguments.Add(typeArgumentProvider);
        }

        return providerFactory.TryCreateProvider(typeArguments.ToImmutable(), out provider);
    }
}