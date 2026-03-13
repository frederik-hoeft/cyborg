using Cyborg.Core.Aot.Modules.Validation.Attributes;

namespace Cyborg.Core.Aot.Modules.Validation.Processors;

internal sealed class DirectoryExistsAttributeProcessor : FilesystemPathAttributeProcessor<DirectoryExistsAttribute>
{
    protected override string AttributeName => nameof(DirectoryExistsAttribute);

    protected override string ErrorCode => "directory_exists";

    protected override string PathKindDisplayName => "directory";

    protected override string BuildExistsExpression() =>
        $"{KnownTypes.Directory}.Exists";
}
