namespace Cyborg.Core.Aot;

internal static class KnownTypes
{
    public static string IServiceProvider => field ??= $"global::{typeof(IServiceProvider).FullName}";

    public static string IReadOnlyCollectionT => field ??= $"global::{typeof(IReadOnlyCollection<>).FullName}";

    public static string CancellationToken => field ??= $"global::{typeof(CancellationToken).FullName}";

    public static string ValueTaskOfT(string typeArgument) => $"global::{typeof(ValueTask<>).Namespace}.{nameof(ValueTask<>)}<{typeArgument}>";

    public static string ListOfT(string typeArgument) => $"global::{typeof(List<>).Namespace}.{nameof(List<>)}<{typeArgument}>";

    public static string TimeSpan => field ??= $"global::{typeof(TimeSpan).FullName}";

    public static string DefaultEqualityComparerOfT(string typeArgument) => $"global::{typeof(EqualityComparer<>).Namespace}.{nameof(EqualityComparer<>)}<{typeArgument}>.{nameof(EqualityComparer<>.Default)}";
}