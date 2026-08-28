using System.Text.Json;

namespace Cyborg.Core.Runtime.Engine.Environments.Syntax;

public interface IChildSyntaxProvider<T> where T : struct, IChildSyntaxProvider<T>
{
    T Child(string segment);

    internal JsonNamingPolicy NamingPolicy { get; }
}
