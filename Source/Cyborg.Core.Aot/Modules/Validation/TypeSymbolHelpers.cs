using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class TypeSymbolHelpers
{
    public const string TaggedStringMetadataName = "Cyborg.Core.Text.TaggedString";

    public const string WellKnownSecretTag = "cyborg.secret.v1";

    public const string TaggedStringGlobalTypeName = "global::Cyborg.Core.Text.TaggedString";

    public const string WellKnownTagsSecretExpression = "global::Cyborg.Core.Text.WellKnownTags.Secret";

    public static bool IsTaggedString(ITypeSymbol type)
    {
        _ = type.TryUnwrapNullableType(out ITypeSymbol unwrapped);
        return unwrapped is INamedTypeSymbol named
            && named.GetFullMetadataName().Equals(TaggedStringMetadataName, StringComparison.Ordinal);
    }

    public static bool IsStringType(ITypeSymbol type)
    {
        _ = type.TryUnwrapNullableType(out ITypeSymbol unwrapped);
        return unwrapped.SpecialType == SpecialType.System_String;
    }

    public static bool IsStringLikeType(ITypeSymbol type) => IsStringType(type) || IsTaggedString(type);

    public static bool RequiresNullGuard(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }
        return !type.IsValueType && (type.IsReferenceType || type.NullableAnnotation == NullableAnnotation.Annotated);
    }

    public static string CreateStringContentExpression(ITypeSymbol type, string accessExpression)
    {
        if (!IsTaggedString(type))
        {
            return accessExpression;
        }
        return RequiresNullGuard(type)
            ? $"{accessExpression}?.Value"
            : $"{accessExpression}.Value";
    }
}
