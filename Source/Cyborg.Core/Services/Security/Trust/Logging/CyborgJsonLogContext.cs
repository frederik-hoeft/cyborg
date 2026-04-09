using System.Text.Json.Serialization;

namespace Cyborg.Core.Services.Security.Trust.Logging;

[JsonSerializable(typeof(IReadOnlyList<ConfigurationTrustPolicyDecision>))]
internal sealed partial class CyborgJsonLogContext : JsonSerializerContext;