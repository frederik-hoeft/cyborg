using Cyborg.Cli.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyborg.Cli;

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, IncludeFields = true)]
[JsonSerializable(typeof(RollingFileLoggingConfiguratorOptions))]
[JsonSerializable(typeof(FileLoggingConfiguratorOptions))]
[JsonSerializable(typeof(ConsoleLoggingConfiguratorOptions))]
internal sealed partial class CliJsonSerializerContext : JsonSerializerContext;