using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Composition;

[Generator(LanguageNames.CSharp)]
public sealed class ModelDecompositionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(EmitFrameworkSource);

        IncrementalValuesProvider<DecompositionAnnotatedTarget> targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(GeneratedDecompositionAttribute).FullName!,
            predicate: static (node, _) => node is RecordDeclarationSyntax or ClassDeclarationSyntax,
            transform: static (attributeContext, _) => DecompositionAnnotatedTarget.Create(attributeContext));

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<DecompositionAnnotatedTarget> Targets)> pipeline =
            context.CompilationProvider.Combine(targets.Collect());

        context.RegisterSourceOutput(pipeline, static (sourceProductionContext, state) =>
        {
            (Compilation compilation, ImmutableArray<DecompositionAnnotatedTarget> discoveredTargets) = state;

            DecompositionContractInfo? contractInfo = DecompositionContractInfo.Create(new ContractExplorer(compilation), sourceProductionContext);
            if (contractInfo is null)
            {
                return;
            }

            foreach (DecompositionAnnotatedTarget target in discoveredTargets)
            {
                DecompositionGenerationCandidate candidate = DecompositionGenerationCandidateFactory.Create(target);

                foreach (Diagnostic diagnostic in candidate.Diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                if (candidate.Model is null)
                {
                    continue;
                }

                string source = ModelDecompositionRenderer.Render(candidate.Model, contractInfo);
                sourceProductionContext.AddSource($"{candidate.Model.TypeSymbol.Name}.Decomposition.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        });
    }

    private static void EmitFrameworkSource(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddEmbeddedSource<DecomposeIgnoreAttribute>();
        context.AddEmbeddedSource<GeneratedDecompositionAttribute>();
    }
}
