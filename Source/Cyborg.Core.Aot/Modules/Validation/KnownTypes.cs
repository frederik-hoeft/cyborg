using System.Reflection.Metadata;

namespace Cyborg.Core.Aot.Modules.Validation;

internal static class KnownTypes
{
    public static string IModuleRuntime => "global::Cyborg.Core.Modules.Runtime.IModuleRuntime";

    public static string IServiceProvider => field ??= $"global::{typeof(IServiceProvider).FullName}";

    public static string CancellationToken => field ??= $"global::{typeof(CancellationToken).FullName}";

    public static string ValueTaskOfT(string typeArgument) => $"global::{typeof(ValueTask<>).Namespace}.{nameof(ValueTask<>)}<{typeArgument}>";

    public static string ValidationResultOfT(string typeArgument) => $"global::{typeof(ValidationResult<>).Namespace}.{nameof(ValidationResult<>)}<{typeArgument}>";

    public static string ListOfT(string typeArgument) => $"global::{typeof(List<>).Namespace}.{nameof(List<>)}<{typeArgument}>";

    public static string IModuleOfT(string typeArgument) => $"global::{typeof(IModule<>).Namespace}.{nameof(IModule<>)}<{typeArgument}>";

    public static string ValidationError => field ??= $"global::{typeof(ValidationError).FullName}";

    public static string TimeSpan => field ??= $"global::{typeof(TimeSpan).FullName}";
}