using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ObjectTypeInspector
{
    public static bool TryDescribe(ITypeSymbol declaredType, [NotNullWhen(true)] out ObjectShape? shape)
    {
        bool isDeclaredNullable = declaredType.TryUnwrapNullableType(out ITypeSymbol nonNullableType);
        if (nonNullableType is not INamedTypeSymbol namedType || !namedType.HasAttribute<ValidatableAttribute>())
        {
            shape = null;
            return false;
        }

        shape = new ObjectShape(namedType, ValueAccessInspector.Describe(declaredType), isDeclaredNullable);
        return true;
    }
}
