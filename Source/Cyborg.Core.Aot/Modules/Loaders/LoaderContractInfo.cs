using Cyborg.Core.Aot.Contracts;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal sealed class LoaderContractInfo(Dictionary<ModuleLoaderFactoryGeneratorContract, INamedTypeSymbol> contractTypes, Compilation compilation) 
    : ContractInfoBase<ModuleLoaderFactoryGeneratorContract>(contractTypes, compilation)
{
    private static readonly ImmutableArray<ModuleLoaderFactoryGeneratorContract> s_allContracts =
    [
        ModuleLoaderFactoryGeneratorContract.IModuleWorker,
        ModuleLoaderFactoryGeneratorContract.ModuleLoaderT,
        ModuleLoaderFactoryGeneratorContract.IModuleWorkerContextT,
        ModuleLoaderFactoryGeneratorContract.ModuleWorkerContextImplementationT,
    ];

    public INamedTypeSymbol IModuleWorker => ContractTypes[ModuleLoaderFactoryGeneratorContract.IModuleWorker];

    public INamedTypeSymbol ModuleLoaderT => ContractTypes[ModuleLoaderFactoryGeneratorContract.ModuleLoaderT];

    public INamedTypeSymbol IModuleWorkerContextT => ContractTypes[ModuleLoaderFactoryGeneratorContract.IModuleWorkerContextT];

    public INamedTypeSymbol ModuleWorkerContextImplementationT => ContractTypes[ModuleLoaderFactoryGeneratorContract.ModuleWorkerContextImplementationT];

    public static LoaderContractInfo? Create(ContractExplorer contractExplorer, SourceProductionContext context)
    {
        Dictionary<ModuleLoaderFactoryGeneratorContract, INamedTypeSymbol>? contracts = FetchContracts(contractExplorer, context, s_allContracts);
        if (contracts is null)
        {
            return null;
        }
        return new LoaderContractInfo(contracts, contractExplorer.Compilation);
    }
}
