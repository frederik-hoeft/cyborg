using Cyborg.Core.Parsing.SyntaxNodes;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;

namespace Cyborg.Core.Runtime.Engine.Expressions.SyntaxNodes;

internal sealed class VariableReferenceNode(VariableReferenceSyntax reference) : ResultSyntaxNodeBase<VariableReferenceSyntax>(name: null, reference);
