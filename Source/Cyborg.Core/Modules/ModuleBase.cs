using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Builders;

namespace Cyborg.Core.Modules;

public abstract record ModuleBase : IModule
{
    [IgnoreOverride]
    [IgnoreInterpolation]
    [VariableIdentifier]
    [Untagged]
    public virtual string? Name { get; init; }

    [IgnoreOverride]
    [IgnoreInterpolation]
    [VariableIdentifier]
    [Untagged]
    public virtual string? Group { get; init; }

    [Required]
    [DefaultInstance]
    public ModuleArtifacts Artifacts { get; init; } = null!;

    public virtual IModuleDescriptor GetDescriptor() => new MinimalModuleDescriptor(this);

    private sealed class MinimalModuleDescriptor(ModuleBase module) : IModuleDescriptor
    {
        public ValueTask DescribeAsync(IObjectDescriptionBuilder descriptionBuilder, CancellationToken cancellationToken)
        {
            descriptionBuilder.AddProperty("$clrtype", module.GetType().FullName);
            descriptionBuilder.AddProperty(nameof(Name), module.Name);
            descriptionBuilder.AddProperty(nameof(Group), module.Group);
            return ValueTask.CompletedTask;
        }
    }
}
