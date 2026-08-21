using Cyborg.Core.Text.Rendering;
using Jab;

namespace Cyborg.Core.Text;

[ServiceProviderModule]
[Singleton<ITaggedStringTagHandler, SecretTagHandler>]
[Singleton<ITaggedStringRenderer, DefaultTaggedStringRenderer>]
[Singleton<ITaggedStringConversionObserver, LoggingTaggedStringConversionObserver>]
public interface ITaggedStringServices;
