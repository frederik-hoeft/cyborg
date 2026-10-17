using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering.Models;

/// <summary>
/// A logical module member path, independent of the C# expression used to produce its runtime key.
/// Property names are known during generation; collection indices are local variables in generated code.
/// </summary>
internal sealed class PreparationPath
{
    private readonly ImmutableArray<Segment> _segments;

    private readonly record struct Segment(string Text, bool IsIndex);

    private PreparationPath(ImmutableArray<Segment> segments) => _segments = segments;

    public static PreparationPath Root(string name) => new([new Segment(name, IsIndex: false)]);

    public PreparationPath AppendProperty(string name) => new(_segments.Add(new Segment($".{name}", IsIndex: false)));

    public PreparationPath AppendIndex(string indexVariable) => new(_segments.Add(new Segment(indexVariable, IsIndex: true)));

    /// <summary>Emits a constant C# string literal, or one interpolated string when the path contains runtime indices.</summary>
    public string RenderExpression()
    {
        if (!_segments.Any(static segment => segment.IsIndex))
        {
            return SymbolDisplay.FormatLiteral(string.Concat(_segments.Select(static segment => segment.Text)), quote: true);
        }

        StringBuilder builder = new("$\"");
        foreach (Segment segment in _segments)
        {
            if (segment.IsIndex)
            {
                builder.Append("[{").Append(segment.Text).Append("}]");
            }
            else
            {
                string literal = SymbolDisplay.FormatLiteral(segment.Text, quote: true);
                builder.Append(literal[1..^1].Replace("{", "{{").Replace("}", "}}"));
            }
        }
        return builder.Append('"').ToString();
    }
}
