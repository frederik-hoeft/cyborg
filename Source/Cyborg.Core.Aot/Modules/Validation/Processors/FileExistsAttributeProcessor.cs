using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class FileExistsAttributeProcessor : FilesystemPathAttributeProcessor<FileExistsAttribute>
{
    protected override string AttributeName => nameof(FileExistsAttribute);

    protected override string ErrorCode => "file_exists";

    protected override string PathKindDisplayName => "file";

    protected override string BuildExistsExpression() =>
        $"{KnownTypes.File}.Exists";
}
