using Cyborg.Core.Runtime.Services.ModuleDescriptors.Model;

namespace Cyborg.Core.Runtime.Services.ModuleDescriptors;

/// <summary>
/// Serializes an immutable module-description tree into a named output format.
/// Implementations may be registered through DI to add application-specific formats.
/// </summary>
public interface IModuleDescriptionSerializer
{
    string Format { get; }

    ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken);
}
