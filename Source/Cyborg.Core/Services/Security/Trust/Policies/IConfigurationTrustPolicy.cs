namespace Cyborg.Core.Services.Security.Trust.Policies;

public interface IConfigurationTrustPolicy
{
    string Name { get; }

    ValueTask<ConfigurationTrustPolicyDecision> EvaluateAsync(IServiceProvider serviceProvider, ConfigurationTrustSubject subject, CancellationToken cancellationToken = default);
}