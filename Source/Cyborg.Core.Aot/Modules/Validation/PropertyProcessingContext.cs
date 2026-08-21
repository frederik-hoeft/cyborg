using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal readonly record struct PropertyProcessingContext
(
    Compilation Compilation,
    INamedTypeSymbol ContainingType,
    IPropertySymbol Property,
    ValidationContractInfo ContractInfo,
    List<Diagnostic> Diagnostics
)
{
    public void Report(DiagnosticDescriptor descriptor, params object[] messageArgs) =>
        Diagnostics.Add(Diagnostic.Create(descriptor, Property.Locations.FirstOrDefault(), messageArgs));
}
