using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Objects;

internal readonly record struct ObjectShapeRenderer(ObjectShape Shape)
{
    public ValueAccess Access(string accessExpression) => Shape.AccessKind.Renderer.Access(accessExpression);
}
