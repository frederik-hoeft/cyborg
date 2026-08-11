using Cyborg.Core.Aot.Contracts;
using System.Text;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Formats short identity strings for modules (module id, name, group).
/// Used by generated <see cref="object.ToString"/> overrides and breakpoint hit banners.
/// </summary>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.ModuleIdentity)]
public static class ModuleIdentity
{
    public static string Format(string moduleId, string? name, string? group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        StringBuilder builder = new(moduleId.Length + 32);
        builder.Append(moduleId);
        if (!string.IsNullOrEmpty(name))
        {
            builder.Append(" name=").Append(name);
        }
        if (!string.IsNullOrEmpty(group))
        {
            builder.Append(" group=").Append(group);
        }
        return builder.ToString();
    }

    public static string Format(string moduleId, IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return Format(moduleId, module.Name, module.Group);
    }
}
