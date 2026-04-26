using Cyborg.Core.Services.Security.Trust.Configuration;
using Cyborg.Core.Services.Security.Trust.Policies;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Core.Services.Security.Trust;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(ConfigurationTrustOptions))]
[JsonSerializable(typeof(UnixPermissionsTrustPolicy))]
[JsonSerializable(typeof(UnixOwnerTrustPolicy))]
[JsonSerializable(typeof(IReadOnlyList<ConfigurationTrustPolicyDecision>))]
public sealed partial class ConfigurationTrustJsonSerializerContext : JsonSerializerContext;
