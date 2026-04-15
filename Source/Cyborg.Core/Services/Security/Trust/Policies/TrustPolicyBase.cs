using System.Text.Json.Serialization;

namespace Cyborg.Core.Services.Security.Trust.Policies;

public abstract record TrustPolicyBase([property: JsonIgnore] string Name) : IConfigurationTrustPolicy
{
    public abstract ValueTask<ConfigurationTrustPolicyDecision> EvaluateAsync(IServiceProvider serviceProvider, ConfigurationTrustSubject subject, CancellationToken cancellationToken = default);

    protected static ValueTask<TResult> ValueTaskOf<TResult>(TResult result) => new(result);
}
