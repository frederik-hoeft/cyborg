using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class MaxLengthAttributeProcessor : LengthAttributeProcessorBase
{
    public override string AttributeMetadataName => typeof(MaxLengthAttribute).FullName!;

    protected override bool TryGetBounds(
        PropertyAttributeProcessingContext context,
        AttributeData attribute,
        out int? min,
        out int? max)
    {
        min = null;
        max = null;

        if (!TryGetSingleIntConstructorArgument(context, attribute, nameof(MaxLengthAttribute), out int maxValue))
        {
            return false;
        }

        max = maxValue;
        return true;
    }
}
