using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal interface IPropertyAttributeProcessor : IPropertyProcessor
{
    string AttributeMetadataName { get; }

    bool TryProcess(PropertyProcessingContext context, AttributeData attribute, out PropertyValidationAspect? aspect);
}
