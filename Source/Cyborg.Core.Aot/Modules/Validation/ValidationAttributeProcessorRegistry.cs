using System.Collections.Frozen;
using System.Collections.Immutable;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ValidationAttributeProcessorRegistry
{
    internal static ImmutableArray<IPropertyAttributeProcessor> All { get; } = 
    [
        new RequiredAttributeProcessor(),
        new DefaultValueAttributeProcessor(),
        new DefaultTimeSpanAttributeProcessor(),
        new RangeAttributeProcessor(),
        new IgnoreOverridesAttributeProcessor(),
    ];

    internal static FrozenDictionary<string, IPropertyAttributeProcessor> ByMetadataName => 
        field ??= All.ToFrozenDictionary(processor => processor.AttributeMetadataName, processor => processor, StringComparer.Ordinal);

    public static bool TryGetProcessor(AttributeData attribute, out IPropertyAttributeProcessor? processor)
    {
        INamedTypeSymbol? attributeClass = attribute.AttributeClass;
        if (attributeClass is null)
        {
            processor = null;
            return false;
        }

        string metadataName = GetFullMetadataName(attributeClass);
        return ByMetadataName.TryGetValue(metadataName, out processor);
    }

    private static string GetFullMetadataName(INamedTypeSymbol type)
    {
        INamedTypeSymbol original = type.OriginalDefinition;

        Stack<string> parts = [];
        ISymbol? current = original;

        while (current is not null)
        {
            switch (current)
            {
                case INamespaceSymbol ns when !ns.IsGlobalNamespace:
                    parts.Push(ns.MetadataName);
                    break;

                case INamedTypeSymbol named:
                    parts.Push(named.MetadataName); // includes `1, `2, etc.
                    break;
            }

            current = current.ContainingSymbol;
        }

        return string.Join(".", parts);
    }
}
