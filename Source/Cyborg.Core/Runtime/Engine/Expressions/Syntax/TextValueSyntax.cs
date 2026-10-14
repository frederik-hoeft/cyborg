using System.Collections.Immutable;

namespace Cyborg.Core.Runtime.Engine.Expressions.Syntax;

/// <summary>
/// Textual value composed of literal, interpolated, and escaped segments in source order.
/// </summary>
internal sealed record TextValueSyntax(ImmutableArray<TextPartSyntax> Parts) : ValueSyntax;
