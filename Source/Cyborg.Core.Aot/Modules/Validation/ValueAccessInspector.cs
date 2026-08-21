using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

/// <summary>
/// Classifies how generated code must access a value while preserving nullable/default absence semantics.
/// </summary>
internal static class ValueAccessInspector
{
    public static ValueAccessKind Describe(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return ValueAccessKind.NullableValue;
        }

        return type.CanEverBeNull
            ? ValueAccessKind.NullGuard
            : ValueAccessKind.Direct;
    }
}
