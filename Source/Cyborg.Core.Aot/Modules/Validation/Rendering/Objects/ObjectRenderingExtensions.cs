using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;

internal static class ObjectRenderingExtensions
{
    extension(ObjectShape self)
    {
        public ObjectShapeRenderer Renderer => new(self);
    }

    extension(ObjectModel self)
    {
        public ObjectRenderer Renderer => new(self);
    }
}
