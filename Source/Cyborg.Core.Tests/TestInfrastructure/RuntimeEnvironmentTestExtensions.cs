using Cyborg.Core.Modules.Runtime.Environments;

namespace Cyborg.Core.Tests.TestInfrastructure;

internal static class RuntimeEnvironmentTestExtensions
{
    public static IEnvironmentLike CreateTestArtifactCollection(this IRuntimeEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new EnvironmentLike(environment.SyntaxFactory, environment.Namespace);
    }
}
