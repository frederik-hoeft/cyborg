using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class MinLengthAttributeProcessor : LengthAttributeProcessorBase
{
    public override string AttributeMetadataName => typeof(MinLengthAttribute).FullName!;

    protected override bool TryGetBounds(
        PropertyProcessingContext context,
        AttributeData attribute,
        out int? min,
        out int? max)
    {
        min = null;
        max = null;

        if (!TryGetSingleIntConstructorArgument(context, attribute, nameof(MinLengthAttribute), out int minValue))
        {
            return false;
        }

        min = minValue;
        return true;
    }
}
