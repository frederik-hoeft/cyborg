using Cyborg.Core.Runtime.Engine.Environments;
using Cyborg.Core.Runtime.Engine.Transactions.Internal;
using Cyborg.Core.Runtime.Model;
using Microsoft.Extensions.Logging;

namespace Cyborg.Core.Runtime.Engine;

/// <summary>
/// Selects environment views by invocation frame or transactional name and creates new logical scopes when requested.
/// The invocation hierarchy is used only for selecting active views, never for reconstructing structural parents.
/// </summary>
internal sealed class EnvironmentScopeSelector(
    RuntimeEnvironmentGraphAccess graph,
    EnvironmentViewMaterializer materializer,
    ILogger logger)
{
    private const string UNBOUND_ENVIRONMENT = "__UNBOUND";

    public IRuntimeEnvironment PrepareEnvironment(
        RuntimeInvocationFrame frame,
        ActiveTransaction active,
        ModuleEnvironment moduleEnvironment,
        IReadOnlyCollection<string>? overrideResolutionTags)
    {
        ArgumentNullException.ThrowIfNull(moduleEnvironment);
        IRuntimeEnvironment environment;
        if (moduleEnvironment.Scope is EnvironmentScope.Reference)
        {
            if (string.IsNullOrEmpty(moduleEnvironment.Name))
            {
                throw new InvalidOperationException("Attempting to reference an environment without providing an environment name.");
            }
            if (!TryGetEnvironment(frame, active, moduleEnvironment.Name, out IRuntimeEnvironment? referencedEnvironment))
            {
                throw new InvalidOperationException($"Attempting to reference an environment that does not exist: {moduleEnvironment.Name}");
            }
            logger.LogNamedEnvironmentResolved(moduleEnvironment.Name);
            environment = referencedEnvironment;
        }
        else
        {
            environment = SelectOrCreate(frame, active, moduleEnvironment.Scope, moduleEnvironment.Name, moduleEnvironment.Transient);
        }

        if (overrideResolutionTags is not null)
        {
            foreach (string tag in overrideResolutionTags)
            {
                if (!frame.Environment.SyntaxFactory.IsValidIdentifier(tag))
                {
                    throw new InvalidOperationException($"Override resolution tags must be valid identifiers: \"{tag}\"");
                }
            }
            logger.LogOverrideTagsApplied(string.Join(", ", overrideResolutionTags), environment.Name);
            environment = environment.WithOverrideResolutionTags(overrideResolutionTags);
        }
        return environment;
    }

    public IRuntimeEnvironment? ResolveEnvironmentReference(RuntimeInvocationFrame frame, ActiveTransaction active, ModuleEnvironmentReference environmentReference)
    {
        ArgumentNullException.ThrowIfNull(environmentReference);
        return environmentReference switch
        {
            { Scope: EnvironmentScopeReference.Current } => frame.Environment,
            { Scope: EnvironmentScopeReference.Global } => frame.GlobalEnvironment,
            { Scope: EnvironmentScopeReference.Parent } => frame.ParentEnvironment,
            { Scope: EnvironmentScopeReference.Reference, Name: { } name } when TryGetEnvironment(frame, active, name, out IRuntimeEnvironment? environment) => environment,
            _ => null
        };
    }

    private IRuntimeEnvironment SelectOrCreate(RuntimeInvocationFrame frame, ActiveTransaction active, EnvironmentScope scope, string? name, bool transient)
    {
        if (string.IsNullOrEmpty(name))
        {
            transient = true;
            name = Guid.CreateVersion7().ToString();
        }
        IRuntimeEnvironment environment = scope switch
        {
            EnvironmentScope.Isolated => CreateNode(active, name, transient, parent: null),
            EnvironmentScope.Global => frame.GlobalEnvironment,
            EnvironmentScope.InheritParent => CreateNode(active, name, transient, RuntimeEnvironmentGraphAccess.CaptureParentView(frame.Environment)),
            EnvironmentScope.InheritGlobal => CreateNode(active, name, transient, RuntimeEnvironmentGraphAccess.CaptureParentView(frame.GlobalEnvironment)),
            EnvironmentScope.Parent => frame.ParentEnvironment.Bind(UNBOUND_ENVIRONMENT),
            EnvironmentScope.Current => frame.Environment.Bind(UNBOUND_ENVIRONMENT),
            EnvironmentScope.Reference => throw new ArgumentException("Attempting to create an environment by reference without providing an environment reference.", nameof(scope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid environment scope."),
        };
        logger.LogEnvironmentCreated(scope.ToString(), environment.Name);
        return environment;
    }

    private IRuntimeEnvironment CreateNode(ActiveTransaction active, string name, bool transient, RuntimeEnvironmentParent? parent)
    {
        RuntimeEnvironmentId environmentId = graph.CreateNode(name, transient, parent, active);
        return materializer.Materialize(environmentId, UNBOUND_ENVIRONMENT, overrideResolutionTags: [], active);
    }

    private bool TryGetEnvironment(RuntimeInvocationFrame frame, ActiveTransaction active, string name, [NotNullWhen(true)] out IRuntimeEnvironment? environment)
    {
        // Invocation-frame names take precedence over transaction-visible registrations.
        // Distinct views of the same logical ID must not be treated as interchangeable.
        for (RuntimeInvocationFrame? candidate = frame; candidate is not null; candidate = candidate.Parent)
        {
            if (!candidate.Environment.Name.Equals(name, StringComparison.Ordinal))
            {
                continue;
            }
            environment = candidate == frame ? candidate.Environment : materializer.BindTransaction(candidate.Environment, active);
            return true;
        }

        if (graph.TryGetNamedEnvironmentId(name, active, out RuntimeEnvironmentId environmentId))
        {
            environment = materializer.Materialize(environmentId, UNBOUND_ENVIRONMENT, overrideResolutionTags: [], active);
            return true;
        }
        environment = null;
        return false;
    }
}
