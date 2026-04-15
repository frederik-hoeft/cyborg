using Cyborg.Core.Aot.Contracts;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed class ValidationContractInfo(Dictionary<ModuleValidationGeneratorContract, INamedTypeSymbol> contractTypes, Compilation compilation) 
    : ContractInfoBase<ModuleValidationGeneratorContract>(contractTypes, compilation)
{
    private static readonly ImmutableArray<ModuleValidationGeneratorContract> s_allContracts =
    [
        ModuleValidationGeneratorContract.IModuleRuntime,
        ModuleValidationGeneratorContract.IModuleT,
        ModuleValidationGeneratorContract.ValidationResultT,
        ModuleValidationGeneratorContract.ValidationError,
        ModuleValidationGeneratorContract.IDefaultValueT,
        ModuleValidationGeneratorContract.IParser,
    ];

    public INamedTypeSymbol IModuleRuntime => ContractTypes[ModuleValidationGeneratorContract.IModuleRuntime];

    public INamedTypeSymbol IModuleT => ContractTypes[ModuleValidationGeneratorContract.IModuleT];

    public INamedTypeSymbol ValidationResultT => ContractTypes[ModuleValidationGeneratorContract.ValidationResultT];

    public INamedTypeSymbol ValidationError => ContractTypes[ModuleValidationGeneratorContract.ValidationError];

    public INamedTypeSymbol IDefaultValueT => ContractTypes[ModuleValidationGeneratorContract.IDefaultValueT];

    public INamedTypeSymbol IParser => ContractTypes[ModuleValidationGeneratorContract.IParser];

    public static ValidationContractInfo? Create(ContractExplorer contractExplorer, SourceProductionContext context)
    {
        Dictionary<ModuleValidationGeneratorContract, INamedTypeSymbol>? contracts = FetchContracts(contractExplorer, context, s_allContracts);
        if (contracts is null)
        {
            return null;
        }
        return new ValidationContractInfo(contracts, contractExplorer.Compilation);
    }
}
