using Cyborg.Core.Aot.Contracts;
using Microsoft.CodeAnalysis;
using System.Collections.Frozen;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal sealed class LoaderContractInfo(Dictionary<ModuleLoaderFactoryGeneratorContract, INamedTypeSymbol> contractTypes) 
    : ContractInfoBase<ModuleLoaderFactoryGeneratorContract>(contractTypes)
{
    private static readonly FrozenSet<ModuleLoaderFactoryGeneratorContract> s_allContracts =
    [
        ModuleLoaderFactoryGeneratorContract.IModuleWorker,
        ModuleLoaderFactoryGeneratorContract.ModuleLoaderT,
    ];

    public INamedTypeSymbol IModuleWorker => ContractTypes[ModuleLoaderFactoryGeneratorContract.IModuleWorker];

    public INamedTypeSymbol ModuleLoaderT => ContractTypes[ModuleLoaderFactoryGeneratorContract.ModuleLoaderT];

    public static LoaderContractInfo? Create(ContractExplorer contractExplorer, SourceProductionContext context)
    {
        Dictionary<ModuleLoaderFactoryGeneratorContract, INamedTypeSymbol>? contracts = FetchContracts(contractExplorer, context, s_allContracts);
        if (contracts is null)
        {
            return null;
        }
        return new LoaderContractInfo(contracts);
    }
}
