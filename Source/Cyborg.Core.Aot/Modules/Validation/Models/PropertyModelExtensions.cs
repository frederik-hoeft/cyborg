using Cyborg.Core.Aot.Modules.Validation.Aspects;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

internal static class PropertyModelExtensions
{
    extension(PropertyModel self)
    {
        public IEnumerable<TAspect> Aspects<TAspect>() where TAspect : class, IPropertyAspect => self.Aspects.OfType<TAspect>();
    }
}
