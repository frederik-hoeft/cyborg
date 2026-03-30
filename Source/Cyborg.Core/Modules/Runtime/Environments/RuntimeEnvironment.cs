using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Modules.Runtime.Environments;

public partial record RuntimeEnvironment(string Name, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace) : EnvironmentLike(SyntaxFactory, Namespace), IRuntimeEnvironment
{
    public IReadOnlyCollection<string> OverrideResolutionTags { get; init; } = [];

    [return: NotNullIfNotNull(nameof(value))]
    public virtual IReadOnlyCollection<T>? ResolveCollection<TModule, T>(TModule module, IReadOnlyCollection<T>? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : ModuleBase, IModule
        => ResolveCollectionCore(this, module, value, moduleExpression, valueExpression);

    [return: NotNullIfNotNull(nameof(value))]
    internal protected virtual IReadOnlyCollection<T>? ResolveCollectionCore<TModule, T>(EnvironmentLike entryPoint, TModule module, IReadOnlyCollection<T>? value, string? moduleExpression, string? valueExpression)
        where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);

        foreach (string identifier in EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
        {
            string overridePath = SyntaxFactory.Path(identifier, valuePath).Override();
            if (!TryResolveVariable(overridePath, entryPoint, out IEnumerable? resolvedValue))
            {
                continue;
            }
            if (resolvedValue is IReadOnlyCollection<T> typedCollection)
            {
                value = typedCollection;
                break;
            }
            value = resolvedValue.Cast<T>().ToImmutableArray();
            break;
        }

        return value;
    }

    [return: NotNullIfNotNull(nameof(value))]
    public virtual T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : ModuleBase, IModule
        => ResolveCore(this, module, value, moduleExpression, valueExpression);

    [return: NotNullIfNotNull(nameof(value))]
    internal protected virtual T? ResolveCore<TModule, T>(EnvironmentLike entryPoint, TModule module, T? value, string? moduleExpression, string? valueExpression)
        where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(entryPoint);
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);

        foreach (string identifier in EnumerateOverrideIdentifiers(module.Name, module.Group, TModule.ModuleId))
        {
            string overridePath = SyntaxFactory.Path(identifier, valuePath).Override();
            if (TryResolveVariable(overridePath, entryPoint, out T? resolvedValue))
            {
                value = resolvedValue;
                break;
            }
        }

        if (value is string stringValue)
        {
            // Handle indirection via string variables
            string resolvedString = Interpolate(stringValue, entryPoint);
            value = Unsafe.As<string, T>(ref resolvedString);
        }

        return value;
    }

    private string ConstructValueResolutionPath<T>(T? value, string? moduleExpression, string? valueExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueExpression);
        if (!valueExpression.StartsWith(moduleExpression, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The value must be provided as a member access expression (e.g. 'MyModule.MyProperty'). Provided value: '{valueExpression}' does not match the expected format.", nameof(value));
        }
        ReadOnlySpan<char> valueSpan = valueExpression.AsSpan()[moduleExpression.Length..];
        if (valueSpan is not ['.', ..] and not ['?', '.', ..])
        {
            throw new ArgumentException($"The value must be provided as a member access expression (e.g. 'MyModule.MyProperty'). Provided value: '{valueExpression}' does not match the expected format.", nameof(value));
        }
        Span<char> cleanedSpan = stackalloc char[valueSpan.Length - 1];
        int skippedChars = 1;
        for (int i = 1; i < valueSpan.Length; i++)
        {
            char c = valueSpan[i];
            if (c == '?')
            {
                skippedChars++;
                continue;
            }
            cleanedSpan[i - skippedChars] = c;
        }
        string valuePath = NamingPolicy.ConvertName(valueSpan.Slice(1, valueSpan.Length - skippedChars).ToString());
        return valuePath;
    }

    public virtual string NamespaceOf<TModule>(TModule module) where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        return GetEffectiveNamespace(module.Name, module.Group, TModule.ModuleId);
    }

    public virtual string NamespaceOf(IModuleWorker module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return GetEffectiveNamespace(module.Module.Name, module.Module.Group, module.ModuleId);
    }

    private IEnumerable<string> EnumerateOverrideIdentifiers(string? name, string? group, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!string.IsNullOrEmpty(name))
        {
            yield return name;
        }
        if (!string.IsNullOrEmpty(group))
        {
            yield return group;
        }
        yield return moduleId;
        foreach (string tag in OverrideResolutionTags)
        {
            yield return tag;
        }
    }

    private static string GetEffectiveNamespace(string? name, string? group, string moduleId) => (name, group) switch
    {
        ({ Length: > 0 }, _) => name,
        (_, { Length: > 0 }) => group,
        _ => moduleId
    };

    void IRuntimeEnvironment.Publish<TModule, T>(TModule module, string root, T decomposable)
    {
        ArgumentNullException.ThrowIfNull(module);
        Publish(root, decomposable, module.Artifacts.DecompositionStrategy, module.Artifacts.PublishNullValues);
    }

    public void Publish(IEnvironmentLike other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach ((string key, object? value) in other)
        {
            SetVariable(key, value);
        }
    }

    public IRuntimeEnvironment Bind(IModuleWorker module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return Bind(NamespaceOf(module));
    }

    public IRuntimeEnvironment Bind(string ns)
    {
        ArgumentNullException.ThrowIfNull(ns);
        return this with
        {
            Namespace = ns
        };
    }

    public IEnvironmentLike CreateArtifactCollection(ModuleArtifacts artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        return new EnvironmentLike(SyntaxFactory, artifacts.Namespace ?? Namespace);
    }

    public IEnvironmentLike CreateArtifactCollection() => new EnvironmentLike(SyntaxFactory, Namespace);

    public IRuntimeEnvironment WithOverrideResolutionTags(IReadOnlyCollection<string> tags) => this with
    {
        OverrideResolutionTags = tags
    };
}
