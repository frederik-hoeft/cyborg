using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Aot.Extensions;
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
        ModuleValidationGeneratorContract.ModuleValidationContext,
        ModuleValidationGeneratorContract.ValidationResult,
        ModuleValidationGeneratorContract.IValidationResultT,
        ModuleValidationGeneratorContract.ValidationError,
        ModuleValidationGeneratorContract.IDefaultValueT,
        ModuleValidationGeneratorContract.IParser,
        ModuleValidationGeneratorContract.IModuleDescriptor,
        ModuleValidationGeneratorContract.IObjectDescriptionBuilder,
        ModuleValidationGeneratorContract.ModuleIdentity,
        ModuleValidationGeneratorContract.TaggedString,
        ModuleValidationGeneratorContract.WellKnownTags,
    ];

    public INamedTypeSymbol IModuleRuntime => ContractTypes[ModuleValidationGeneratorContract.IModuleRuntime];

    public INamedTypeSymbol IModuleT => ContractTypes[ModuleValidationGeneratorContract.IModuleT];

    public INamedTypeSymbol ModuleValidationContext => ContractTypes[ModuleValidationGeneratorContract.ModuleValidationContext];

    public INamedTypeSymbol ValidationResult => ContractTypes[ModuleValidationGeneratorContract.ValidationResult];

    public INamedTypeSymbol IValidationResultT => ContractTypes[ModuleValidationGeneratorContract.IValidationResultT];

    public INamedTypeSymbol ValidationError => ContractTypes[ModuleValidationGeneratorContract.ValidationError];

    public INamedTypeSymbol IDefaultValueT => ContractTypes[ModuleValidationGeneratorContract.IDefaultValueT];

    public INamedTypeSymbol IParser => ContractTypes[ModuleValidationGeneratorContract.IParser];

    public INamedTypeSymbol IModuleDescriptor => ContractTypes[ModuleValidationGeneratorContract.IModuleDescriptor];

    public INamedTypeSymbol IObjectDescriptionBuilder => ContractTypes[ModuleValidationGeneratorContract.IObjectDescriptionBuilder];

    public INamedTypeSymbol ModuleIdentity => ContractTypes[ModuleValidationGeneratorContract.ModuleIdentity];

    public INamedTypeSymbol TaggedString => ContractTypes[ModuleValidationGeneratorContract.TaggedString];

    public INamedTypeSymbol WellKnownTags => ContractTypes[ModuleValidationGeneratorContract.WellKnownTags];

    public string SecretTagExpression => $"{WellKnownTags.RenderGlobal()}.{GetRequiredConstantField(WellKnownTags, "SECRET").Name}";

    public string SecretTag => (string)(GetRequiredConstantField(WellKnownTags, "SECRET").ConstantValue
        ?? throw new InvalidOperationException("The registered WellKnownTags.SECRET member is not a compile-time constant."));

    public static ValidationContractInfo? Create(ContractExplorer contractExplorer, SourceProductionContext context)
    {
        Dictionary<ModuleValidationGeneratorContract, INamedTypeSymbol>? contracts = FetchContracts(contractExplorer, context, s_allContracts);
        if (contracts is null)
        {
            return null;
        }

        return new ValidationContractInfo(contracts, contractExplorer.Compilation);
    }

    private static IFieldSymbol GetRequiredConstantField(INamedTypeSymbol type, string memberName) =>
        type.GetMembers(memberName).OfType<IFieldSymbol>().SingleOrDefault(static field => field.HasConstantValue)
        ?? throw new InvalidOperationException($"Registered contract type '{type.ToDisplayString()}' must expose constant field '{memberName}'.");
}
