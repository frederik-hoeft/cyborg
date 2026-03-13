using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class IgnoreOverridesAttributeProcessor : IPropertyAttributeProcessor
{
    public string AttributeMetadataName => typeof(IgnoreOverridesAttribute).FullName;

    public bool TryProcess(PropertyAttributeProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect)
    {
        _ = context;
        _ = attribute;
        aspect = new IgnoreOverridesAspect();
        return true;
    }

    private sealed class IgnoreOverridesAspect : PropertyValidationAspect
    {
        public override bool EnsuresDefault => false;

        public override string? RewriteOverrideResolutionExpression(PropertyRewriteContext context, string? currentExpression) => null;
    }
}
