using Cyborg.Core.Configuration.Model;

namespace Cyborg.Core.Configuration;

public interface IConfigurationSource
{
    IReadOnlyCollection<DynamicKeyValuePair> Options { get; }
}
