namespace Cyborg.Cli.Arguments;

internal readonly record struct DynamicArgumentDefinition(string Key, string? TypeName, string Value);
