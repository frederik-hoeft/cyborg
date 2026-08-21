using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Core.Aot.Modules.Validation.Rendering;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;

internal readonly record struct ObjectShapeRenderer(ObjectShape Shape)
{
    public ValueAccess Access(string accessExpression) => Shape.AccessKind.Renderer.Access(accessExpression);
}
