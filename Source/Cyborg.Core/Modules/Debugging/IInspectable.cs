using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Descriptors;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Provides a full recursive description of a module's validated configuration state.
/// Implementations are source-generated for types annotated with <c>[GeneratedModuleValidation]</c>.
/// </summary>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IInspectable)]
public interface IInspectable : IModuleDescriptor
{
    /// <summary>
    /// Returns the module description as human-readable plain text.
    /// </summary>
    string Inspect();
}
