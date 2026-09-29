using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Services.Validation;

namespace Cyborg.Core.Runtime.Model;

/// <summary>Per-module transaction policy. An omitted <see cref="OnError"/> uses the process default.</summary>
[Validatable]
public sealed record ModuleTransactionSettings(
    [property: DefinedEnumValue] TransactionOnError? OnError
) : IDefaultInstance<ModuleTransactionSettings>
{
    /// <summary>No module-local policy. Resolution falls through to the global default.</summary>
    public static ModuleTransactionSettings Default { get; } = new(OnError: null);
}
