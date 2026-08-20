using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed record ValidationAnnotatedTarget(INamedTypeSymbol TypeSymbol)
{
    public static ValidationAnnotatedTarget Create(GeneratorAttributeSyntaxContext context) =>
        new((INamedTypeSymbol)context.TargetSymbol);
}
