using Cyborg.Core.Aot.Contracts;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Configuration.Model;

[GeneratorContractRegistration<ModelDecompositionGeneratorContract>(ModelDecompositionGeneratorContract.DynamicKeyValuePair)]
public sealed record DynamicKeyValuePair(string Key, [property: JsonIgnore] object? Value);
