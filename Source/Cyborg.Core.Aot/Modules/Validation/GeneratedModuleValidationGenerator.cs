using System.Text;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Cyborg.Core.Aot.Modules.Validation;

[Generator(LanguageNames.CSharp)]
public sealed class GeneratedModuleValidationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ValidationFrameworkSourceRegistry.Emit);

        IncrementalValuesProvider<GenerationCandidate?> candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(GeneratedModuleValidationAttribute).FullName,
            predicate: static (node, _) => node is RecordDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (attributeContext, cancellationToken) => GenerationCandidateFactory.Create(attributeContext, cancellationToken));

        IncrementalValuesProvider<GenerationCandidate> validCandidates = candidates
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!);

        context.RegisterSourceOutput(validCandidates, static (sourceProductionContext, candidate) =>
        {
            foreach (Diagnostic diagnostic in candidate.Diagnostics)
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }

            if (candidate.Model is null)
            {
                return;
            }

            string source = ModuleValidationRenderer.Render(candidate.Model);
            sourceProductionContext.AddSource($"{candidate.Model.HintName}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}
