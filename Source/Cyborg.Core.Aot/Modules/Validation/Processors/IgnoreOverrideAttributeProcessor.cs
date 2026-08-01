using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class IgnoreOverrideAttributeProcessor : AttributeProcessorBase<IgnoreOverrideAttribute>
{
    public override bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect)
    {
        if (!TryGetConstructorArgumentValue(attribute, argumentIndex: 0, in context, out bool recurse))
        {
            return false.WithDefaults(out aspect);
        }
        aspect = new IgnoreOverrideAspect(recurse);
        return true;
    }
}

internal sealed class IgnoreOverrideAspect(bool recurse) : PropertyAspect
{
    public bool Recurse => recurse;
}
