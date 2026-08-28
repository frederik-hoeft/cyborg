using Cyborg.Core.Runtime.Engine.Environments;

namespace Cyborg.Core.Runtime.Extensions;

[SuppressMessage("Design", CA1034, Justification = CA1034_JUSTIFY_EXTENSION_SYNTAX_CSHARP_14)]
public static class EnvironmentLikeExtensions
{
    extension(IEnvironmentLike self)
    {
        public void SetVariable<T>(ReadOnlySpan<char> name, T value) => self.SetVariable(name.ToString(), value);
    }
}
