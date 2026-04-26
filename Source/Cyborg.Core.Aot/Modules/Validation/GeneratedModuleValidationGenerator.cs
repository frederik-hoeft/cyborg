using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

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
            transform: static (attributeContext, cancellationToken) => GenerationCandidateFactory.Create(attributeContext));

        IncrementalValuesProvider<GenerationCandidate> validCandidates = candidates
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!);

        IncrementalValueProvider<(Compilation, ImmutableArray<GenerationCandidate>)> compilationAndCandidates =
            context.CompilationProvider.Combine(validCandidates.Collect());

        context.RegisterSourceOutput(compilationAndCandidates, static (sourceProductionContext, compilationAndCandidates) =>
        {
            (Compilation compilation, ImmutableArray<GenerationCandidate> candidates) = compilationAndCandidates;
            ContractExplorer explorer = new(compilation);
            ValidationContractInfo? contractInfo = ValidationContractInfo.Create(explorer, sourceProductionContext);
            if (contractInfo is null)
            {
                return;
            }
            foreach (GenerationCandidate candidate in candidates)
            {
                foreach (Diagnostic diagnostic in candidate.Diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                if (candidate.Model is null)
                {
                    continue;
                }
                DiagnosticsReporter diagnosticsReporter = new([]);
                string source = ModuleValidationRenderer.Render(candidate.Model, contractInfo, diagnosticsReporter);
                foreach (Diagnostic diagnostic in diagnosticsReporter.Diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }
                sourceProductionContext.AddSource($"{candidate.Model.HintName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        });
    }
}
