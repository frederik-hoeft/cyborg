using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal interface IPropertyAttributeProcessor : IPropertyProcessor
{
    string AttributeMetadataName { get; }

    bool TryProcess(AttributeData attribute, ref readonly PropertyProcessingContext context, out PropertyAspect? aspect);
}
