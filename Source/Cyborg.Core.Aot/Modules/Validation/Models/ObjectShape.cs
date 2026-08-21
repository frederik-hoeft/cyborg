using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

/// <summary>
/// Describes how generated code obtains a usable value for a nested [Validatable] object.
/// </summary>
internal sealed record ObjectShape
(
    INamedTypeSymbol Type,
    ValueAccessKind AccessKind,
    bool IsDeclaredNullable
)
{
    /// <summary>
    /// True when generated code guards a non-nullable reference defensively and must restore nullable flow state afterwards.
    /// </summary>
    public bool RequiresNullableFlowRelaxation => AccessKind == ValueAccessKind.NullGuard && !IsDeclaredNullable;
}
