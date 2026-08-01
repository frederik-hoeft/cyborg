using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal abstract class AttributeProcessorBase<TAttribute> : AttributeProcessorBase where TAttribute : Attribute
{
    public override string AttributeMetadataName => typeof(TAttribute).FullName;
}
