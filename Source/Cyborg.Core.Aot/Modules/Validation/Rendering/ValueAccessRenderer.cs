using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal readonly record struct ValueAccessRenderer(ValueAccessKind Kind)
{
    public ValueAccess Access(string accessExpression) => Kind switch
    {
        ValueAccessKind.Direct => new ValueAccess(null, accessExpression),
        ValueAccessKind.NullGuard => new ValueAccess($"{accessExpression} is not null", accessExpression),
        ValueAccessKind.NullableValue => new ValueAccess($"{accessExpression} is not null", $"{accessExpression}.Value"),
        _ => throw new InvalidOperationException($"Unsupported value access kind '{Kind}'."),
    };
}

internal static class ValueAccessRenderingExtensions
{
    extension(ValueAccessKind self)
    {
        public ValueAccessRenderer Renderer => new(self);
    }
}
