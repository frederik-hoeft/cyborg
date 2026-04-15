using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Composition;

internal sealed record DecompositionAnnotatedTarget(INamedTypeSymbol TypeSymbol, AttributeData GeneratorAttribute)
{
    public static DecompositionAnnotatedTarget Create(GeneratorAttributeSyntaxContext context) =>
        new((INamedTypeSymbol)context.TargetSymbol, context.Attributes[0]);
}
