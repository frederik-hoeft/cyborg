using Cyborg.Core.Aot.Modules.Composition;
using Cyborg.Core.Aot.Modules.Validation.Attributes;
using Cyborg.Core.Modules.Validation;

namespace Cyborg.Modules.Borg.Create.Model;

[Validatable]
[GeneratedDecomposition]
public sealed partial record BorgFilesCacheSentinelOptions
(
    bool Enabled,
    [property: Required][property: UnrootedPath][property: NormalizedPath][property: DefaultValue<string>(BorgFilesCacheSentinelOptions.DEFAULT_ARCHIVE_PATH)] string ArchivePath,
    [property: Required][property: FileName][property: DefaultValue<string>(BorgFilesCacheSentinelOptions.DEFAULT_SENTINEL_FILE_NAME)] string SentinelFileName
) : IDefaultInstance<BorgFilesCacheSentinelOptions>
{
    private const string DEFAULT_ARCHIVE_PATH = ".cyborg";
    private const string DEFAULT_SENTINEL_FILE_NAME = "borg_files_cache.sentinel";

    public static BorgFilesCacheSentinelOptions Default { get; } = new(Enabled: false, ArchivePath: DEFAULT_ARCHIVE_PATH, SentinelFileName: DEFAULT_SENTINEL_FILE_NAME);
}
