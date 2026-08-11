using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using System.Globalization;

namespace Cyborg.Core.Aot;

internal static class KnownTypes
{
    public static string IServiceProvider => field ??= typeof(IServiceProvider).RenderGlobal();

    public static string CancellationToken => field ??= typeof(CancellationToken).RenderGlobal();

    public static string ValueTask => field ??= typeof(ValueTask).RenderGlobal();

    public static string ValueTaskOfT(string typeArgument) => typeof(ValueTask<>).RenderGlobalWithGenerics(typeArgument);

    public static string IEnumerableOfT(string typeArgument) => typeof(IEnumerable<>).RenderGlobalWithGenerics(typeArgument);

    public static string ICollectionOfT(string typeArgument) => typeof(ICollection<>).RenderGlobalWithGenerics(typeArgument);

    public static string ListOfT(string typeArgument) => typeof(List<>).RenderGlobalWithGenerics(typeArgument);

    public static string Enumerable => "global::System.Linq.Enumerable";

    public static string ImmutableArray => "global::System.Collections.Immutable.ImmutableArray";

    public static string TimeSpan => field ??= typeof(TimeSpan).RenderGlobal();

    public static string Enum => field ??= typeof(Enum).RenderGlobal();

    public static string JsonNamingPolicy => "global::System.Text.Json.JsonNamingPolicy";

    public static string NotNullAttribute => "global::System.Diagnostics.CodeAnalysis.NotNullAttribute";

    public static string GeneratedRegexAttribute => "global::System.Text.RegularExpressions.GeneratedRegexAttribute";

    public static string Directory => $"global::System.IO.Directory";

    public static string File => $"global::System.IO.File";

    public static string Path => field ??= typeof(Path).RenderGlobal();

    public static string ArgumentNullException => field ??= typeof(ArgumentNullException).RenderGlobal();

    public static string Task => field ??= typeof(Task).RenderGlobal();

    public static string InvariantCulture => field ??= $"{typeof(CultureInfo).RenderGlobal()}.{nameof(CultureInfo.InvariantCulture)}";

    public static string ValidationRuntimeHelpers => field ??= typeof(ValidationRuntimeHelpers).RenderGlobal();

    public static string DefaultEqualityComparerOfT(string typeArgument) => $"{typeof(EqualityComparer<>).RenderGlobalWithGenerics(typeArgument)}.{nameof(EqualityComparer<>.Default)}";
}
