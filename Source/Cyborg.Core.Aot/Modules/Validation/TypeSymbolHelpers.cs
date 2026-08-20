using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class TypeSymbolHelpers
{
    public static bool IsTaggedString(ITypeSymbol type, ValidationContractInfo contractInfo)
    {
        ArgumentNullException.ThrowIfNull(contractInfo);
        _ = type.TryUnwrapNullableType(out ITypeSymbol unwrapped);
        return SymbolEqualityComparer.Default.Equals(unwrapped, contractInfo.TaggedString);
    }

    public static bool IsStringType(ITypeSymbol type)
    {
        _ = type.TryUnwrapNullableType(out ITypeSymbol unwrapped);
        return unwrapped.SpecialType == SpecialType.System_String;
    }

    public static bool IsStringLikeType(ITypeSymbol type, ValidationContractInfo contractInfo) =>
        IsStringType(type) || IsTaggedString(type, contractInfo);

    public static bool RequiresNullGuard(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }
        return !type.IsValueType && (type.IsReferenceType || type.NullableAnnotation == NullableAnnotation.Annotated);
    }

    public static string CreateStringContentExpression(ITypeSymbol type, ValidationContractInfo contractInfo, string accessExpression)
    {
        if (!IsTaggedString(type, contractInfo))
        {
            return accessExpression;
        }
        return RequiresNullGuard(type)
            ? $"{accessExpression}?.Value"
            : $"{accessExpression}.Value";
    }
}
