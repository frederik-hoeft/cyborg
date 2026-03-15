using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class CollectionTypeInspector
{
    private const string IMMUTABLE_ARRAY_METADATA_NAME = "System.Collections.Immutable.ImmutableArray`1";

    public static bool TryDescribe(Compilation compilation, ITypeSymbol type, out CollectionTypeDescriptor? descriptor)
    {
        descriptor = null;

        if (type is IArrayTypeSymbol arrayType)
        {
            descriptor = new CollectionTypeDescriptor(
                ElementType: arrayType.ElementType,
                MaterializationKind: CollectionMaterializationKind.UseArray,
                MaterializationTypeName: null);

            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (!TryGetEnumerableInterface(namedType, out INamedTypeSymbol? enumerableInterface))
        {
            return false;
        }

        ITypeSymbol elementType = enumerableInterface.TypeArguments[0];
        CollectionMaterializationKind materializationKind = DetermineMaterializationKind(compilation, namedType, elementType, out string? materializationTypeName);

        descriptor = new CollectionTypeDescriptor(
            ElementType: elementType,
            MaterializationKind: materializationKind,
            MaterializationTypeName: materializationTypeName);

        return true;
    }

    private static bool TryGetEnumerableInterface(INamedTypeSymbol type, [NotNullWhen(true)] out INamedTypeSymbol? enumerableInterface)
    {
        enumerableInterface = null;

        if (IsEnumerable(type))
        {
            enumerableInterface = type;
            return true;
        }

        foreach (INamedTypeSymbol candidate in type.AllInterfaces)
        {
            if (IsEnumerable(candidate))
            {
                enumerableInterface = candidate;
                return true;
            }
        }

        return false;
    }

    private static CollectionMaterializationKind DetermineMaterializationKind(Compilation compilation, INamedTypeSymbol type, ITypeSymbol elementType, out string? materializationTypeName)
    {
        materializationTypeName = null;

        if (type.OriginalDefinition.GetFullMetadataName().Equals(typeof(List<>).FullName, StringComparison.Ordinal))
        {
            return CollectionMaterializationKind.UseList;
        }

        if (type.IsGenericType && type.OriginalDefinition.GetFullMetadataName() == IMMUTABLE_ARRAY_METADATA_NAME)
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

        if (HasPublicParameterlessConstructor(type) && ImplementsCollection(type))
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

        INamedTypeSymbol? listDefinition = compilation.GetTypeByMetadataName(typeof(List<>).FullName!);
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
            if (!conversion.Exists)
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

    private static bool ImplementsCollection(INamedTypeSymbol type)
    {
        if (IsCollection(type))
        {
            return true;
        }

        foreach (INamedTypeSymbol candidate in type.AllInterfaces)
        {
            if (IsCollection(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnumerable(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;

    private static bool IsCollection(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T;

    internal sealed record CollectionTypeDescriptor(
        ITypeSymbol ElementType,
        CollectionMaterializationKind MaterializationKind,
        string? MaterializationTypeName);
}
