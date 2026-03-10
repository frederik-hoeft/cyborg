using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Contracts;

internal sealed class ContractExplorer(Compilation compilation)
{
    public Dictionary<TContract, INamedTypeSymbol> GetContracts<TContract>(SourceProductionContext context) where TContract : unmanaged, Enum
    {
        IEnumerable<INamedTypeSymbol> allTypes = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .Union([compilation.Assembly], SymbolEqualityComparer.Default)
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IAssemblySymbol>()
            .Where(asm => asm.Name.StartsWith(nameof(Cyborg)))
            .SelectMany(asm => asm.GlobalNamespace.GetAllTypes());

        Dictionary<TContract, INamedTypeSymbol> contractTypes = [];
        foreach (INamedTypeSymbol type in allTypes)
        {
            AttributeData? contractRegistration = type.GetAttributes().FirstOrDefault(a => a.AttributeClass is
            {
                IsGenericType: true,
                IsUnboundGenericType: false
            } attrClass && attrClass.ConstructUnboundGenericType().GetFullMetadataName().Equals(typeof(GeneratorContractRegistrationAttribute<>).FullName, StringComparison.Ordinal));
            if (contractRegistration is not { AttributeClass.TypeArguments: [INamedTypeSymbol contractType] })
            {
                continue;
            }
            if (contractType.GetFullMetadataName().Equals(typeof(TContract).FullName, StringComparison.Ordinal))
            {
                TContract contractValue = (TContract)contractRegistration.ConstructorArguments[0].Value!;
                if (contractTypes.ContainsKey(contractValue))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateContract,
                        Location.None,
                        contractValue,
                        contractTypes[contractValue]?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining)),
                        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining))));
                    continue;
                }
                contractTypes[contractValue] = type;
            }
        }
        return contractTypes;
    }
}