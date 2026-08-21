using Cyborg.Core.Aot.Modules.Validation.Aspects;
using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal interface IPropertyAttributeProcessor : IPropertyProcessor
{
    string AttributeMetadataName { get; }

    bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect);
}
