using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class CollectionTypeInspector
{
    public static bool TryDescribe(Compilation compilation, ITypeSymbol type, [NotNullWhen(true)] out CollectionShape? shape)
    {
        shape = null;
        _ = type.TryUnwrapNullableType(out ITypeSymbol nonNullableType);

        if (nonNullableType.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (nonNullableType is IArrayTypeSymbol arrayType)
        {
            shape = new CollectionShape(
                ElementType: arrayType.ElementType,
                AccessKind: DetermineAccessKind(type, isImmutableArray: false),
                ElementAccessKind: DetermineElementAccessKind(arrayType.ElementType),
                CountKind: CollectionCountKind.ArrayLength,
                CountInterface: null,
                MaterializationKind: CollectionMaterializationKind.UseArray,
                MaterializationTypeName: null);

            return true;
        }

        if (nonNullableType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (!TryGetGenericInterface(namedType, SpecialType.System_Collections_Generic_IEnumerable_T, out INamedTypeSymbol? enumerableInterface))
        {
            return false;
        }

        ITypeSymbol elementType = enumerableInterface.TypeArguments[0];
        CollectionMaterializationKind materializationKind = DetermineMaterializationKind(compilation, namedType, elementType, out string? materializationTypeName);
        bool isImmutableArray = IsImmutableArray(namedType);
        CollectionCountKind countKind = TryGetGenericInterface(namedType, SpecialType.System_Collections_Generic_IReadOnlyCollection_T, out INamedTypeSymbol? countInterface)
            ? CollectionCountKind.ReadOnlyCollection
            : CollectionCountKind.None;

        shape = new CollectionShape(
            ElementType: elementType,
            AccessKind: DetermineAccessKind(type, isImmutableArray),
            ElementAccessKind: DetermineElementAccessKind(elementType),
            CountKind: countKind,
            CountInterface: countInterface,
            MaterializationKind: materializationKind,
            MaterializationTypeName: materializationTypeName);

        return true;
    }

    private static CollectionAccessKind DetermineAccessKind(ITypeSymbol declaredType, bool isImmutableArray)
    {
        bool isNullableValueType = declaredType is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        };

        if (isImmutableArray)
        {
            return isNullableValueType
                ? CollectionAccessKind.NullableImmutableArray
                : CollectionAccessKind.ImmutableArray;
        }

        if (isNullableValueType)
        {
            return CollectionAccessKind.NullableValue;
        }

        return declaredType.CanEverBeNull
            ? CollectionAccessKind.NullGuard
            : CollectionAccessKind.Direct;
    }

    private static CollectionElementAccessKind DetermineElementAccessKind(ITypeSymbol elementType)
    {
        if (elementType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return CollectionElementAccessKind.NullableValue;
        }

        return elementType.CanEverBeNull
            ? CollectionElementAccessKind.NullGuard
            : CollectionElementAccessKind.Direct;
    }

    private static bool TryGetGenericInterface(INamedTypeSymbol type, SpecialType interfaceSpecialType, [NotNullWhen(true)] out INamedTypeSymbol? interfaceType)
    {
        if (type.OriginalDefinition.SpecialType == interfaceSpecialType)
        {
            interfaceType = type;
            return true;
        }

        foreach (INamedTypeSymbol candidate in type.AllInterfaces)
        {
            if (candidate.OriginalDefinition.SpecialType == interfaceSpecialType)
            {
                interfaceType = candidate;
                return true;
            }
        }

        interfaceType = null;
        return false;
    }

    private static CollectionMaterializationKind DetermineMaterializationKind(Compilation compilation, INamedTypeSymbol type, ITypeSymbol elementType, out string? materializationTypeName)
    {
        materializationTypeName = null;

        if (type.OriginalDefinition.GetFullMetadataName().Equals(typeof(List<>).FullName, StringComparison.Ordinal))
        {
            return CollectionMaterializationKind.UseList;
        }

        if (IsImmutableArray(type))
        {
            return CollectionMaterializationKind.UseImmutableArray;
        }

        if (type.TypeKind == TypeKind.Interface)
        {
            return type.OriginalDefinition.SpecialType switch
            {
                SpecialType.System_Collections_Generic_IEnumerable_T => CollectionMaterializationKind.UseList,
                SpecialType.System_Collections_Generic_ICollection_T => CollectionMaterializationKind.UseList,
                SpecialType.System_Collections_Generic_IList_T => CollectionMaterializationKind.UseList,
                SpecialType.System_Collections_Generic_IReadOnlyCollection_T => CollectionMaterializationKind.UseList,
                SpecialType.System_Collections_Generic_IReadOnlyList_T => CollectionMaterializationKind.UseList,
                _ => CollectionMaterializationKind.None,
            };
        }

        if (TryGetSingleParameterListConstructor(compilation, type, elementType, out string? constructibleTypeName))
        {
            materializationTypeName = constructibleTypeName;
            return CollectionMaterializationKind.ConstructFromList;
        }

        if (!type.IsAbstract && HasPublicParameterlessConstructor(type) && ImplementsCollection(type))
        {
            materializationTypeName = type.ToDisplayString(KnownSymbolFormats.NonNullable);
            return CollectionMaterializationKind.ParameterlessAdd;
        }

        return CollectionMaterializationKind.None;
    }

    private static bool TryGetSingleParameterListConstructor(Compilation compilation, INamedTypeSymbol type, ITypeSymbol elementType, out string? constructibleTypeName)
    {
        constructibleTypeName = null;

        if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        INamedTypeSymbol? listDefinition = compilation.GetTypeByMetadataName(typeof(List<>).FullName);
        if (listDefinition is null)
        {
            return false;
        }

        INamedTypeSymbol constructedListType = listDefinition.Construct(elementType);

        foreach (IMethodSymbol constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public || constructor.Parameters.Length != 1)
            {
                continue;
            }

            Conversion conversion = compilation.ClassifyConversion(constructedListType, constructor.Parameters[0].Type);
            if (!conversion.IsImplicit)
            {
                continue;
            }

            constructibleTypeName = type.ToDisplayString(KnownSymbolFormats.NonNullable);
            return true;
        }

        return false;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        foreach (IMethodSymbol constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsCollection(INamedTypeSymbol type) =>
        TryGetGenericInterface(type, SpecialType.System_Collections_Generic_ICollection_T, out _);

    private static bool IsImmutableArray(INamedTypeSymbol type) =>
        type.IsGenericType && type.OriginalDefinition.GetFullMetadataName().Equals(typeof(ImmutableArray<>).FullName, StringComparison.Ordinal);
}
