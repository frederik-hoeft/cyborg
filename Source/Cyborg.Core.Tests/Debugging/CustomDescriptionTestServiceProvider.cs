using Cyborg.Core.Modules.Descriptors;
using Cyborg.Core.Modules.Descriptors.Model;
using Jab;

namespace Cyborg.Core.Tests.Debugging;

[ServiceProvider]
[Import<IModuleDescriptionServices>]
[Singleton<IModuleDescriptionSerializer>(Factory = nameof(CreateCustomSerializer))]
internal sealed partial class CustomDescriptionTestServiceProvider
{
    private static IModuleDescriptionSerializer CreateCustomSerializer() => new CustomSerializer();

    private sealed class CustomSerializer : IModuleDescriptionSerializer
    {
        public string Format => "application/x-cyborg-test";

        public ValueTask<string> SerializeAsync(IDescriptionObjectComponent description, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(description);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult("custom-from-di");
        }
    }
}
