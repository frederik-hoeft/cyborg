using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Modules.Descriptors;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Provides a full recursive description of a module's validated configuration state.
/// Implementations are source-generated for types annotated with
/// <c>[GeneratedModuleValidation]</c>.
/// </summary>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IInspectable)]
public interface IInspectable : IModuleDescriptor
{
    /// <summary>
    /// Returns a multi-line, human-readable dump of the module's identity and property state.
    /// </summary>
    string Inspect();
}
