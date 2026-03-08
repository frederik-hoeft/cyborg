namespace Cyborg.Core.Modules.Configuration.Model;

public interface IDecomposable
{
    IEnumerable<DynamicKeyValuePair> Decompose();
}