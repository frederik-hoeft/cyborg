namespace Cyborg.Core.Services.Security.Trust;

public interface IConfigurationTrustMonitor
{
    ValueTask<ConfigurationTrustDecision> EvaluateAsync(string path, CancellationToken cancellationToken = default);
}