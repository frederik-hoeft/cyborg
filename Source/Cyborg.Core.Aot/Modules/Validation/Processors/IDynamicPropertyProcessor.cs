using Cyborg.Core.Aot.Modules.Validation.Aspects;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal interface IDynamicPropertyProcessor : IPropertyProcessor
{
    bool TryProcess(ref readonly PropertyProcessingContext context, out IPropertyAspect? aspect);
}
