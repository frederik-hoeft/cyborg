using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class ReadOnlyCollectionOverrideProcessor : IDynamicPropertyProcessor
{
    public bool TryProcess(PropertyProcessingContext context, out PropertyValidationAspect? aspect)
    {
        _ = context;
        if (context.Property.Type is not INamedTypeSymbol { IsGenericType: true } propertyType || propertyType.ConstructedFrom.SpecialType != SpecialType.System_Collections_Generic_IReadOnlyCollection_T)
        {
            aspect = null;
            return true;
        }
        aspect = new ReadOnlyCollectionOverridesAspect();
        return true;
    }

    private sealed class ReadOnlyCollectionOverridesAspect : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        public override string? RewriteOverrideResolutionExpression(PropertyRewriteContext context, string? currentExpression, string rootPathExpression) =>
            $"runtime.Environment.ResolveCollection({context.ModuleVariable}, {context.PropertyAccessExpression}, valueExpression: \"{rootPathExpression}\")";
    }
}
