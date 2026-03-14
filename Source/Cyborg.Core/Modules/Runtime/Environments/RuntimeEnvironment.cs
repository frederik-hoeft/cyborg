using Cyborg.Core.Modules.Configuration.Model;
using Cyborg.Core.Modules.Runtime.Environments.Syntax;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Cyborg.Core.Modules.Runtime.Environments;

public partial record RuntimeEnvironment(string Name, bool IsTransient, VariableSyntaxBuilder SyntaxFactory, string Namespace) : EnvironmentLike(SyntaxFactory, Namespace), IRuntimeEnvironment
{
    [return: NotNullIfNotNull(nameof(value))]
    public virtual IReadOnlyCollection<T>? ResolveCollection<TModule, T>(TModule module, IReadOnlyCollection<T>? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);
        string overridePath = SyntaxFactory.Path(NamespaceOf(module), valuePath).Override();
        if (TryResolveVariable(overridePath, out IEnumerable? resolvedValue))
        {
            if (resolvedValue is IReadOnlyCollection<T> typedCollection)
            {
                value = typedCollection;
            }
            else
            {
                value = resolvedValue.Cast<T>().ToImmutableArray();
            }
        }
        return value;
    }

    [return: NotNullIfNotNull(nameof(value))]
    public virtual T? Resolve<TModule, T>(TModule module, T? value, [CallerArgumentExpression(nameof(module))] string? moduleExpression = null, [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
        where TModule : ModuleBase, IModule
    {
        ArgumentNullException.ThrowIfNull(module);
        string valuePath = ConstructValueResolutionPath(value, moduleExpression, valueExpression);
        string overridePath = SyntaxFactory.Path(NamespaceOf(module), valuePath).Override();
        if (TryResolveVariable(overridePath, out T? resolvedValue))
        {
            value = resolvedValue;
        }
        else if (value is string stringValue)
        {
            // Handle indirection via string variables
            string resolvedString = Interpolate(stringValue);
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
        return GetEffectiveNamespace(module.Name, TModule.ModuleId);
    }

    public virtual string NamespaceOf(IModuleWorker module)
    {
        ArgumentNullException.ThrowIfNull(module);
        return GetEffectiveNamespace(module.Module.Name, module.ModuleId);
    }

    private static string GetEffectiveNamespace(string? name, string moduleId) => !string.IsNullOrEmpty(name) ? name : moduleId;

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
}