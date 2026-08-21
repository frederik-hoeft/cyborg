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

        IncrementalValuesProvider<ValidationAnnotatedTarget> targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(GeneratedModuleValidationAttribute).FullName!,
            predicate: static (node, _) => node is RecordDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (attributeContext, _) => ValidationAnnotatedTarget.Create(attributeContext));

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<ValidationAnnotatedTarget> Targets)> pipeline =
            context.CompilationProvider.Combine(targets.Collect());

        context.RegisterSourceOutput(pipeline, static (sourceProductionContext, state) =>
        {
            (Compilation compilation, ImmutableArray<ValidationAnnotatedTarget> discoveredTargets) = state;
            ValidationContractInfo? contractInfo = ValidationContractInfo.Create(new ContractExplorer(compilation), sourceProductionContext);
            if (contractInfo is null)
            {
                return;
            }

            foreach (ValidationAnnotatedTarget target in discoveredTargets)
            {
                GenerationCandidate candidate = GenerationCandidateFactory.Create(target, contractInfo);
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
