namespace Cyborg.TestModules.Activation;

public sealed class ActivationProbeDependency(string identity)
{
    public string Identity { get; } = identity;
}
