using Microsoft.CodeAnalysis.CSharp;

namespace Cyborg.Core.Aot.Modules.Validation.Models;

/// <summary>
/// Describes a user-facing path to a validation target independently of generated access expressions.
/// Collection indices are represented by generated runtime expressions and remain composable through deeper recursion.
/// </summary>
internal readonly record struct ValidationPath(string Template, bool RequiresInterpolation)
{
    public string Expression => RequiresInterpolation
        ? $"$\"{Template}\""
        : SymbolDisplay.FormatLiteral(Template, quote: true);

    public string Description => $"Property '{Template}'";

    public static ValidationPath ForProperty(string propertyName) => new(propertyName, RequiresInterpolation: false);

    public ValidationPath AppendProperty(string propertyName) => new($"{Template}.{propertyName}", RequiresInterpolation);

    public ValidationPath AppendElement(string indexExpression) => new($"{Template}[{{{indexExpression}}}]", RequiresInterpolation: true);
}
