namespace Cyborg.Core.Aot;

internal static class KnownTypes
{
    public static string IServiceProvider => field ??= $"global::{typeof(IServiceProvider).FullName}";

    public static string CancellationToken => field ??= $"global::{typeof(CancellationToken).FullName}";

    public static string ValueTaskOfT(string typeArgument) => $"global::{typeof(ValueTask<>).Namespace}.{nameof(ValueTask<>)}<{typeArgument}>";

    public static string IEnumerableOfT(string typeArgument) => $"global::{typeof(IEnumerable<>).Namespace}.{nameof(IEnumerable<>)}<{typeArgument}>";

    public static string ListOfT(string typeArgument) => $"global::{typeof(List<>).Namespace}.{nameof(List<>)}<{typeArgument}>";

    public static string TimeSpan => field ??= $"global::{typeof(TimeSpan).FullName}";

    public static string Enum => field ??= $"global::{typeof(Enum).FullName}";

    public static string JsonNamingPolicy => "global::System.Text.Json.JsonNamingPolicy";

    public static string NotNullAttribute => "global::System.Diagnostics.CodeAnalysis.NotNullAttribute";

    public static string GeneratedRegexAttribute => "global::System.Text.RegularExpressions.GeneratedRegexAttribute";

    public static string Directory => "global::System.IO.Directory";

    public static string File => "global::System.IO.File";

    public static string DefaultEqualityComparerOfT(string typeArgument) => $"global::{typeof(EqualityComparer<>).Namespace}.{nameof(EqualityComparer<>)}<{typeArgument}>.{nameof(EqualityComparer<>.Default)}";
}