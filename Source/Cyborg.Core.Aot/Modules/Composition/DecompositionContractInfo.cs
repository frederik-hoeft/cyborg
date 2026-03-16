using Cyborg.Core.Aot.Contracts;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Composition;

internal sealed class DecompositionContractInfo(Dictionary<ModelDecompositionGeneratorContract, INamedTypeSymbol> contractTypes, Compilation compilation)
    : ContractInfoBase<ModelDecompositionGeneratorContract>(contractTypes, compilation)
{
    private static readonly ImmutableArray<ModelDecompositionGeneratorContract> s_allContracts =
    [
        ModelDecompositionGeneratorContract.IDecomposable,
        ModelDecompositionGeneratorContract.DynamicKeyValuePair,
    ];

    public INamedTypeSymbol IDecomposable => ContractTypes[ModelDecompositionGeneratorContract.IDecomposable];

    public INamedTypeSymbol DynamicKeyValuePair => ContractTypes[ModelDecompositionGeneratorContract.DynamicKeyValuePair];

    public static DecompositionContractInfo? Create(ContractExplorer explorer, SourceProductionContext context)
    {
        Dictionary<ModelDecompositionGeneratorContract, INamedTypeSymbol>? contracts = FetchContracts(explorer, context, s_allContracts);
        if (contracts is null)
        {
            return null;
        }

        return new DecompositionContractInfo(contracts, explorer.Compilation);
    }
}
