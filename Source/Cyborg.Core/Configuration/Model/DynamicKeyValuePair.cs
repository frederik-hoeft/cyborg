using Cyborg.Core.Aot.Contracts;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration.Model;

[Validatable]
[GeneratorContractRegistration<ModelDecompositionGeneratorContract>(ModelDecompositionGeneratorContract.DynamicKeyValuePair)]
public sealed record DynamicKeyValuePair([property: Required][property: Untagged] string Key, [property: Required][property: JsonIgnore] object? Value);
