using Cyborg.Core.Modules.Configuration.Serialization;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Modules.Configuration.Serialization.Dynamics;

public interface IDynamicGenericValueProviderFactory
{
    string TypeName { get; }

    int Arity { get; }

    bool TryCreateProvider(
        ImmutableArray<IDynamicValueProvider> typeArguments,
        [NotNullWhen(true)] out IDynamicValueProvider? provider);
}