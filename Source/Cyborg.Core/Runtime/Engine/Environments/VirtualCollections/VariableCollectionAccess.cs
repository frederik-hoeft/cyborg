using Cyborg.Core.Runtime.Engine.Environments.Syntax;
using System.Text.RegularExpressions;

namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

internal readonly record struct VariableCollectionAccess(string Name, VariableCollectionAccessKind Kind)
{
    public static bool TryParse(VariableSyntaxBuilder syntax, string? text, out VariableCollectionAccess access)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        if (text is not null)
        {
            Match match = syntax.CollectionAccessRegex.Match(text);
            if (match.Success)
            {
                access = new VariableCollectionAccess(match.Groups["name"].Value, ParseSuffix(match.Groups["suffix"].Value));
                return true;
            }
        }

        access = default;
        return false;
    }

    public string FormatSnapshotName() => Name + "[]";

    private static VariableCollectionAccessKind ParseSuffix(string suffix) => suffix switch
    {
        "[]+" => VariableCollectionAccessKind.Append,
        "[+]" => VariableCollectionAccessKind.Lazy,
        "[]" => VariableCollectionAccessKind.Snapshot,
        _ => throw new ArgumentOutOfRangeException(nameof(suffix), suffix, "Unsupported virtual collection suffix.")
    };
}
