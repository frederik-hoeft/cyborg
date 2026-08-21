using Cyborg.Core.Aot.Modules.Validation.Models;
using Cyborg.Shared.Text;

namespace Cyborg.Core.Aot.Modules.Validation.Rendering;

internal interface ISectionRenderer
{
    void RenderSection(IndentedStringBuilder builder, ModuleModel model);
}
