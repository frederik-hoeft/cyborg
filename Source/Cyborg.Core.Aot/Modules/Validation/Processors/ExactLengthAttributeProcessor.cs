using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class ExactLengthAttributeProcessor : LengthAttributeProcessorBase
{
    public override string AttributeMetadataName => typeof(ExactLengthAttribute).FullName!;

    protected override bool TryGetBounds(
        PropertyProcessingContext context,
        AttributeData attribute,
        out int? min,
        out int? max)
    {
        min = null;
        max = null;

        if (!TryGetSingleIntConstructorArgument(context, attribute, nameof(ExactLengthAttribute), out int length))
        {
            return false;
        }

        min = length;
        max = length;
        return true;
    }
}