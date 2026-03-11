using Cyborg.Core.Aot.Modules.Validation.Model;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class LengthAttributeProcessor : LengthAttributeProcessorBase
{
    public override string AttributeMetadataName => typeof(LengthAttribute).FullName!;

    protected override bool TryGetBounds(
        PropertyAttributeProcessingContext context,
        AttributeData attribute,
        out int? min,
        out int? max)
    {
        min = null;
        max = null;

        if (!TryGetTwoIntConstructorArguments(context, attribute, nameof(LengthAttribute), out int minValue, out int maxValue))
        {
            return false;
        }

        min = minValue;
        max = maxValue;
        return true;
    }
}