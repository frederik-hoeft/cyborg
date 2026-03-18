namespace Cyborg.Modules.Borg.Shared.Json.Create;

public sealed record BorgEncryptionInfo(
    string Mode,
    string? Keyfile = null
);
