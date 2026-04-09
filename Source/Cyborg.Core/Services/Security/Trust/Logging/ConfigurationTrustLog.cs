using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZLogger;

namespace Cyborg.Core.Services.Security.Trust.Logging;

public static partial class ConfigurationTrustLog
{
    [ZLoggerMessage(LogLevel.Information, "Audit success: configuration at path '{path}' is trusted. Total policies evaluated: {decisionCount}")]
    public static partial void LogConfigurationTrustAuditSuccess(this ILogger logger, string path, int decisionCount);

    [ZLoggerMessage(LogLevel.Information, "Audit failed: configuration at path '{path}' is not trusted. Total policies evaluated: {decisionCount}, total policies rejected: {rejectedCount}, first rejected policy: {firstRejectedPolicy}, reason: {firstRejectedReason}")]
    public static partial void LogConfigurationTrustAuditFailure(this ILogger logger, string path, int decisionCount, int rejectedCount, string firstRejectedPolicy, string firstRejectedReason);

    [ZLoggerMessage(LogLevel.Debug, "Audited configuration at path '{path}'. Trusted: {success}. Total policies evaluated: {policyCount}. Decisions: {decisionJson}")]
    public static partial void LogConfigurationTrustDetails(this ILogger logger, string path, bool success, int policyCount, string decisionJson);

    public static void LogConfigurationTrustDecision(this ILogger logger, ConfigurationTrustDecision decision)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(decision);

        int rejectedCount = 0;
        string? firstRejectedPolicy = null;
        string? firstRejectedReason = null;

        foreach (ConfigurationTrustPolicyDecision policyDecision in decision.Decisions)
        {
            if (policyDecision.Decision is not ConfigurationTrustDecisionKind.Accept)
            {
                ++rejectedCount;

                if (firstRejectedPolicy is null)
                {
                    firstRejectedPolicy = policyDecision.PolicyName;
                    firstRejectedReason = policyDecision.Reason;
                }
            }
        }
        if (logger.IsEnabled(LogLevel.Debug))
        {
            string payloadJson = JsonSerializer.Serialize(decision, CyborgJsonLogContext.Default.IReadOnlyListConfigurationTrustPolicyDecision);
            logger.LogConfigurationTrustDetails(decision.Path, decision.IsTrusted, decision.Decisions.Count, payloadJson);
        }
        if (decision.IsTrusted)
        {
            logger.LogConfigurationTrustAuditSuccess(decision.Path, decision.Decisions.Count);
        }
        else
        {
            logger.LogConfigurationTrustAuditFailure(decision.Path, decision.Decisions.Count, rejectedCount, firstRejectedPolicy ?? "N/A", firstRejectedReason ?? "N/A");
        }
    }
}