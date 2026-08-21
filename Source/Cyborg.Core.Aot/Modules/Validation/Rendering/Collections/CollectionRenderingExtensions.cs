using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Collections;

internal static class CollectionRenderingExtensions
{
    extension(CollectionShape self)
    {
        public CollectionShapeRenderer Renderer => new(self);
    }

    extension(CollectionModel self)
    {
        public CollectionRenderer Renderer => new(self);
    }
}
