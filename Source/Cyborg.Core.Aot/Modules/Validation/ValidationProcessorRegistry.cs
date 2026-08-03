using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Processors;
using Microsoft.CodeAnalysis;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class ValidationProcessorRegistry
{
    internal static ImmutableArray<IPropertyProcessor> All { get; } =
    [
        new RequiredProcessor(),
        new DefaultValueProcessor(),
        new DefaultTimeSpanProcessor(),
        new RangeProcessor(),
        new IgnoreOverrideProcessor(),
        new IgnoreInterpolationProcessor(),
        new LengthProcessor(),
        new MinLengthProcessor(),
        new MaxLengthProcessor(),
        new ExactLengthProcessor(),
        new DefinedEnumValueProcessor(),
        new DefaultInstanceProcessor(),
        new MatchesRegexProcessor(),
        new FileExistsProcessor(),
        new DirectoryExistsProcessor(),
        new ReadOnlyCollectionOverrideProcessor(),
        new MatchesGrammarProcessor(),
        new DefaultInstanceFactoryProcessor(),
        new FileNameProcessor(),
        new RootedPathProcessor(),
        new UnrootedPathProcessor(),
        new NormalizedPathProcessor(),
        new VariableIdentifierProcessor(),
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

    public static bool TryProcess(ref readonly PropertyProcessingContext context, out ImmutableArray<PropertyAspect> aspects)
    {
        ImmutableArray<PropertyAspect>.Builder aspectBuilder = ImmutableArray.CreateBuilder<PropertyAspect>();

        foreach (AttributeData attribute in context.Property.GetAttributes())
        {
            if (!TryGetProcessor(attribute, out IPropertyAttributeProcessor? processor) || processor is null)
            {
                continue;
            }
            if (!processor.TryProcess(attribute, in context, out PropertyAspect? aspect))
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
            if (!processor.TryProcess(in context, out PropertyAspect? aspect))
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
