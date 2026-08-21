using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class ReadOnlyCollectionOverrideProcessor : IDynamicPropertyProcessor
{
    public bool TryProcess(ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect)
    {
        aspect = null;
        if (context.Property.Type is INamedTypeSymbol { IsGenericType: true } propertyType && propertyType.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyCollection_T)
        {
            aspect = new ReadOnlyCollectionOverridesAspect();
        }
        // readonly collection overrides are optional, since not every property is a collection
        return true;
    }

    private sealed class ReadOnlyCollectionOverridesAspect : IPropertyOverrideAspect
    {
        public string RewriteOverrideResolutionExpression(PropertyRewriteContext context, string currentExpression, string rootPathExpression) =>
            $"{context.ContextVariable}.ResolveCollectionOverride({context.ModuleVariable}, {context.PropertyAccessExpression}, moduleExpression: \"{context.ModuleVariable}\", valueExpression: \"{rootPathExpression}\")";
    }
}
