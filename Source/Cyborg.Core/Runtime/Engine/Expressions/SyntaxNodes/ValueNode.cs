using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;

/// <summary>Typed parser result containing an immutable value syntax node.</summary>
internal sealed class ValueNode(ValueSyntax syntax) : ResultSyntaxNodeBase<ValueSyntax>(name: null, syntax);
