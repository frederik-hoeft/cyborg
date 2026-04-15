namespace Cyborg.Modules.Borg.Create.Metrics;

public sealed record BorgEncryptionInfo(
    string Mode,
    string? Keyfile = null
);
