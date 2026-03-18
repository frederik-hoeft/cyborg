using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Modules.Borg;

[Validatable]
[GeneratedDecomposition]
public sealed partial record BorgRemoteRepository
(
    [property: Required][property: DefaultValue<string>("ssh://")] string Protocol,
    [property: Required] string Username,
    [property: Required] string Hostname,
    [property: Required][property: Range<int>(Min = 1, Max = ushort.MaxValue)] int Port,
    string? RepositoryRoot,
    [property: Required] string RepositoryName
)
{
    public string GetRepositoryUri() => $"{Protocol}{Username}@{Hostname}:{Port}{RepositoryRoot}/{RepositoryName}";
}