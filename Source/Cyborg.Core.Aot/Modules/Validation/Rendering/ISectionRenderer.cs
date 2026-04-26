using Cyborg.Core.Aot.Extensions;
using Cyborg.Core.Aot.Modules.Validation.Models;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal interface ISectionRenderer
{
    void RenderSection(IndentedStringBuilder builder, ModuleModel model);
}
