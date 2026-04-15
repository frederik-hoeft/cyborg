global using System.Diagnostics.CodeAnalysis;
global using static Cyborg.Core.GlobalUsings;

namespace Cyborg.Core;

public static class GlobalUsings
{
    public const string CA1034 = "CA1034:Nested types should not be visible";
    public const string CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14 = "false positive for C# 14 extension types, which must be nested within a static class.";
}