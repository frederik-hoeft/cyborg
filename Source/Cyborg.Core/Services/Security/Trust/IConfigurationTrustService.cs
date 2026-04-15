namespace Cyborg.Core.Services.Security.Trust;

public interface IConfigurationTrustService
{
    void Enforce(ConfigurationTrustDecision decision);
}
