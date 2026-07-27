using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Provides a full recursive serialization of a module's configuration state for debugging.
/// Implementations are source-generated for types annotated with <c>[GeneratedModuleValidation]</c>.
/// </summary>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.IInspectable)]
// TODO: since the validation source generator is now doing more than just validation, we should rename it accordingly
public interface IInspectable
{
    /// <summary>
    /// Returns a multi-line, human-readable dump of the module's identity and property state.
    /// </summary>
    // CONSIDER: refactor so support multiple output formats (e.g. JSON, YAML, etc.) for easier consumption by other tools (consume a formatter service here or something)
    string Inspect();
}
