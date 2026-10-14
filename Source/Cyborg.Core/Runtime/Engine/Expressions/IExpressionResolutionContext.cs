using Cyborg.Core.Runtime.Engine.Expressions.Evaluated;
using Cyborg.Core.Runtime.Engine.Expressions.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Core.Runtime.Engine.Expressions;

/// <summary>
/// Resolves a reference using the current execution view. Implementations own inheritance, entry-point routing, and cycle detection.
/// They return evaluated structural values, not text to be interpreted again. A missing or null binding returns false.
/// </summary>
internal interface IExpressionResolutionContext
{
    bool TryResolve(VariableReferenceSyntax reference, [NotNullWhen(true)] out EvaluatedValue? value);
}
