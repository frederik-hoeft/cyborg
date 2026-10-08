using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class IgnoreValueExpressionProcessor : AttributeProcessorBase<IgnoreValueExpressionAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect)
    {
        if (!ValidateStringLikePropertyType(attribute, in context))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new IgnoreValueExpressionAspect();
        return true;
    }
}

internal sealed class IgnoreValueExpressionAspect : IPropertyAspect;
