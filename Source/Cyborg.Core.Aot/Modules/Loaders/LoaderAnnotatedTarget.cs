using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal sealed record LoaderAnnotatedTarget(INamedTypeSymbol TypeSymbol, AttributeData GeneratorAttribute)
{
    public static LoaderAnnotatedTarget Create(GeneratorAttributeSyntaxContext context) =>
        new((INamedTypeSymbol)context.TargetSymbol, context.Attributes[0]);
}