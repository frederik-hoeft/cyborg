using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Contracts;

internal sealed record ContractDiscoveryResult<TContract>(
    Dictionary<TContract, INamedTypeSymbol> Contracts,
    ImmutableArray<Diagnostic> Diagnostics)
    where TContract : unmanaged, Enum;