using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Contracts;

[Generator(LanguageNames.CSharp)]
public sealed class ContractRegistrationBootstrapGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) => context.RegisterPostInitializationOutput(EmitFrameworkSources);

    private static void EmitFrameworkSources(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddEmbeddedSource<ModuleValidationGeneratorContract>();
        context.AddEmbeddedSource<ModuleLoaderFactoryGeneratorContract>();
        context.AddEmbeddedSource(typeof(GeneratorContractRegistrationAttribute<>));
    }
}
