using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.ModuleDescriptors;
using Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

namespace Cyborg.Core.Runtime;

public abstract record ModuleBase : IModule
{
    [IgnoreOverride]
    [IgnoreInterpolation]
    [IgnoreValueExpression]
    [VariableIdentifier]
    [Untagged]
    public virtual string? Name { get; init; }

    [IgnoreOverride]
    [IgnoreInterpolation]
    [IgnoreValueExpression]
    [VariableIdentifier]
    [Untagged]
    public virtual string? Group { get; init; }

    [Required]
    [DefaultInstance]
    public ModuleArtifacts Artifacts { get; init; } = null!;

    /// <summary>
    /// Optional failure-publication policy. When <see cref="ModuleTransactionSettings.OnError"/> is omitted, the process default applies.
    /// </summary>
    [IgnoreOverride(recurse: true)]
    [DefaultInstance]
    public ModuleTransactionSettings Transaction { get; init; } = null!;

    public virtual IModuleDescriptor GetDescriptor() => new MinimalModuleDescriptor(this);

    private sealed class MinimalModuleDescriptor(ModuleBase module) : IModuleDescriptor
    {
        public ValueTask DescribeAsync(IObjectDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken)
        {
            descriptionBuilder.AddProperty("$clrtype", module.GetType().FullName);
            descriptionBuilder.AddProperty(nameof(Name), module.Name);
            descriptionBuilder.AddProperty(nameof(Group), module.Group);
            descriptionBuilder.AddProperty("transaction.on_error", module.Transaction?.OnError?.ToString());
            return ValueTask.CompletedTask;
        }
    }
}
