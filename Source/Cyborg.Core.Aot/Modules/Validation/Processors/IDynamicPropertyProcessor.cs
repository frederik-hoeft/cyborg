namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal interface IDynamicPropertyProcessor : IPropertyProcessor
{
    bool TryProcess(PropertyProcessingContext context, out PropertyValidationAspect? aspect);
}
