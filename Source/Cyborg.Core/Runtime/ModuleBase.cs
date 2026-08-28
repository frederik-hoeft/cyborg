using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Runtime.Model;
using Cyborg.Core.Runtime.Services.ModuleDescriptors;
using Cyborg.Core.Runtime.Services.ModuleDescriptors.Builders;

namespace Cyborg.Core.Runtime;

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
