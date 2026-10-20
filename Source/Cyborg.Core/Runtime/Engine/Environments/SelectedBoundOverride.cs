using Cyborg.Core.Runtime.Engine.Expressions.Bound;

namespace Cyborg.Core.Runtime.Engine.Environments;

/// <summary>
/// A raw override and the structural view containing its stored expression.
/// The source view and the original lookup entry point are independent evaluation contexts.
/// </summary>
internal sealed record SelectedBoundOverride(BoundValue Value, EnvironmentLike Source);
