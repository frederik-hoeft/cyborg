using System.Collections.Frozen;
using System.Collections.Immutable;
using Cyborg.Core.Aot.Extensions;
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

        string metadataName = attributeClass.GetFullMetadataName();
        return ByMetadataName.TryGetValue(metadataName, out processor);
    }
}
