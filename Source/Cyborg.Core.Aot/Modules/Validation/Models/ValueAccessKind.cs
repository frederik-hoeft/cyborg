namespace Cyborg.Core.Aot.Modules.Validation.Models;

/// <summary>
/// Describes how generated code obtains a usable value while preserving null/default absence semantics.
/// </summary>
internal enum ValueAccessKind
{
    Direct = 0,
    NullGuard,
    NullableValue,
}
