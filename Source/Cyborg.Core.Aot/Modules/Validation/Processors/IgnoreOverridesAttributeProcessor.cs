using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class IgnoreOverridesAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(IgnoreOverridesAttribute).FullName;

    public bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyAspect? aspect)
    {
        _ = context;
        _ = attribute;
        aspect = new IgnoreOverridesAspect();
        return true;
    }

    private sealed class IgnoreOverridesAspect : PropertyAspect
    {
        public override string? RewriteOverrideResolutionExpression(PropertyRewriteContext context, string? currentExpression, string rootPathExpression) => null;
    }
}
