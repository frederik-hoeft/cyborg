using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ValidationProcessorRegistry
{
    internal static ImmutableArray<IPropertyProcessor> All { get; } = 
    [
        new RequiredAttributeProcessor(),
        new DefaultValueAttributeProcessor(),
        new DefaultTimeSpanAttributeProcessor(),
        new RangeAttributeProcessor(),
        new IgnoreOverridesAttributeProcessor(),
        new LengthAttributeProcessor(),
        new MinLengthAttributeProcessor(),
        new MaxLengthAttributeProcessor(),
        new ExactLengthAttributeProcessor(),
        new DefinedEnumValueAttributeProcessor(),
        new DefaultInstanceAttributeProcessor(),
        new MatchesRegexAttributeProcessor(),
        new FileExistsAttributeProcessor(),
        new DirectoryExistsAttributeProcessor(),
        new ReadOnlyCollectionOverrideProcessor(),
        new MatchesGrammarAttributeProcessor(),
    ];

    private static FrozenDictionary<string, IPropertyAttributeProcessor> ByMetadataName => 
        field ??= All.OfType<IPropertyAttributeProcessor>().ToFrozenDictionary(processor => processor.AttributeMetadataName, processor => processor, StringComparer.Ordinal);

    private static ImmutableArray<IDynamicPropertyProcessor> DynamicProcessors =>
        field.IsDefault ? field = [.. All.OfType<IDynamicPropertyProcessor>()] : field;

    private static bool TryGetProcessor(AttributeData attribute, out IPropertyAttributeProcessor? processor)
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

    public static bool TryProcess(PropertyProcessingContext context, out ImmutableArray<PropertyValidationAspect> aspects)
    {
        ImmutableArray<PropertyValidationAspect>.Builder aspectBuilder = ImmutableArray.CreateBuilder<PropertyValidationAspect>();

        foreach (AttributeData attribute in context.Property.GetAttributes())
        {
            if (!TryGetProcessor(attribute, out IPropertyAttributeProcessor? processor) || processor is null)
            {
                continue;
            }
            if (!processor.TryProcess(context, attribute, out PropertyValidationAspect? aspect))
            {
                return false;
            }
            if (aspect is not null)
            {
                aspectBuilder.Add(aspect);
            }
        }
        foreach (IDynamicPropertyProcessor processor in DynamicProcessors)
        {
            if (!processor.TryProcess(context, out PropertyValidationAspect? aspect))
            {
                return false;
            }
            if (aspect is not null)
            {
                aspectBuilder.Add(aspect);
            }
        }
        aspects = aspectBuilder.ToImmutable();
        return true;
    }
}
