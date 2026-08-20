using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed class GenerationCandidateFactory
{
    public Compilation Compilation { get; }

    public INamedTypeSymbol TypeSymbol { get; }

    public ValidationContractInfo ContractInfo { get; }

    private GenerationCandidateFactory(Compilation compilation, INamedTypeSymbol typeSymbol, ValidationContractInfo contractInfo)
    {
        Compilation = compilation;
        TypeSymbol = typeSymbol;
        ContractInfo = contractInfo;
    }

    private GenerationCandidate Create()
    {
        List<Diagnostic> diagnostics = [];
        PropertyModelBuilder builder = new(this, diagnostics);
        ImmutableArray<PropertyModel> properties = builder.Build();

        string ns = TypeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : TypeSymbol.ContainingNamespace.ToDisplayString();

        ImmutableArray<ContainingTypeModel> containingTypes = BuildContainingTypes();
        string fullyQualifiedTypeName = TypeSymbol.ToDisplayString(KnownSymbolFormats.Nullable);
        string hintName = TypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('.', '_');

        ModuleModel model = new(ModuleSymbol: TypeSymbol, Namespace: ns, TypeName: TypeSymbol.Name, FullyQualifiedTypeName: fullyQualifiedTypeName,
            HintName: hintName + ".ModuleValidation", ContainingTypes: containingTypes, Properties: properties);

        return new GenerationCandidate(model.HintName, model, [.. diagnostics]);
    }

    private ImmutableArray<ContainingTypeModel> BuildContainingTypes()
    {
        Stack<ContainingTypeModel> stack = new();
        INamedTypeSymbol? current = TypeSymbol.ContainingType;

        while (current is not null)
        {
            string keyword = current.IsRecord ? "partial record" : "partial class";
            stack.Push(new ContainingTypeModel($"{keyword} {current.Name}"));
            current = current.ContainingType;
        }

        return [.. stack];
    }

    public static GenerationCandidate Create(ValidationAnnotatedTarget target, ValidationContractInfo contractInfo)
    {
        INamedTypeSymbol typeSymbol = target.TypeSymbol;
        if (!typeSymbol.IsRecord)
        {
            return CreateFailureCandidate(typeSymbol, ValidationGeneratorDiagnostics.TypeMustBeRecord);
        }
        if (!typeSymbol.HasPartialDeclaration())
        {
            return CreateFailureCandidate(typeSymbol, ValidationGeneratorDiagnostics.TypeMustBePartial);
        }

        GenerationCandidateFactory factory = new(contractInfo.Compilation, typeSymbol, contractInfo);
        return factory.Create();
    }

    private static GenerationCandidate CreateFailureCandidate(INamedTypeSymbol symbol, DiagnosticDescriptor descriptor) =>
        new(symbol.ToDisplayString(), Model: null, [Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), symbol.Name)]);
}
