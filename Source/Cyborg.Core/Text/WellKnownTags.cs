using Cyborg.Core.Aot.Contracts;

namespace Cyborg.Core.Text;

/// <summary>
/// Well-known tag names that Cyborg interprets globally.
/// </summary>
[GeneratorContractRegistration<ModuleValidationGeneratorContract>(ModuleValidationGeneratorContract.WellKnownTags)]
public static class WellKnownTags
{
    /// <summary>
    /// Marks a string as a secret. Renderers redact values carrying this tag.
    /// </summary>
    public const string SECRET = "cyborg.secret.v1";
}
