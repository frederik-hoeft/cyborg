using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class LengthAttributeProcessor : LengthAttributeProcessorBase<LengthAttribute>
{
    protected override bool TryGetBounds(AttributeData attribute, ref readonly PropertyProcessingContext context, out int? min, out int? max)
    {
        if (!TryGetConstructorArgumentValue(attribute, argumentIndex: 0, in context, out int minValue)
            || !TryGetConstructorArgumentValue(attribute, argumentIndex: 1, in context, out int maxValue))
        {
            return false.WithDefaults(out min, out max);
        }

        min = minValue;
        max = maxValue;
        return true;
    }
}
