using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Loaders.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Loaders;

[Generator(LanguageNames.CSharp)]
public sealed class ModuleLoaderFactoryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInit =>
            postInit.AddEmbeddedSource<GeneratedModuleLoaderFactoryAttribute>());

        IncrementalValuesProvider<LoaderAnnotatedTarget> targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: typeof(GeneratedModuleLoaderFactoryAttribute).FullName!,
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (attributeContext, _) => LoaderAnnotatedTarget.Create(attributeContext));

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<LoaderAnnotatedTarget> Targets)> pipeline =
            context.CompilationProvider.Combine(targets.Collect());

        context.RegisterSourceOutput(pipeline, static (spc, state) =>
        {
            (Compilation compilation, ImmutableArray<LoaderAnnotatedTarget> discoveredTargets) = state;

            LoaderContractInfo? contractInfo = LoaderContractInfo.Create(new ContractExplorer(compilation), spc);
            if (contractInfo is null)
            {
                return;
            }

            foreach (LoaderAnnotatedTarget target in discoveredTargets)
            {
                LoaderGenerationCandidate candidate = LoaderGenerationCandidateFactory.Create(target, contractInfo);

                foreach (Diagnostic diagnostic in candidate.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic);
                }

                if (candidate.Model is { } model)
                {
                    string source = LoaderFactoryRenderer.Render(model);
                    spc.AddSource($"{model.ClassSymbol.Name}.ModuleLoaderFactory.g.cs", source);
                }
            }
        });
    }
}