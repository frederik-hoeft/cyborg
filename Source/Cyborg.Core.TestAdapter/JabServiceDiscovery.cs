using Jab;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace Cyborg.Core.TestAdapter;

public sealed class JabServiceDiscovery : IJabServiceDiscovery
{
    // caches for reflection-based lookups to avoid repeated reflection costs
    private static readonly ConcurrentDictionary<CacheKey, PropertyInfo?> s_instancePropertyCache = [];
    private static readonly ConcurrentDictionary<CacheKey, MethodInfo?> s_factoryMethodCache = [];
    // Names and namespace derived from typeof so they track Jab's actual declarations.
    private static readonly string s_jabNamespace = typeof(ImportAttribute<>).Namespace!;
    private static readonly string s_importAttribute = typeof(ImportAttribute<>).Name;

    // must match by name, since Jab generates internal attribute types per assembly, so the actual Type objects are different
    private static readonly (string OneTArg, string TwoTArgs, ServiceLifetime Lifetime)[] s_lifetimeMappings =
    [
        (typeof(SingletonAttribute<>).Name,  typeof(SingletonAttribute<,>).Name,  ServiceLifetime.Singleton),
        (typeof(ScopedAttribute<>).Name,     typeof(ScopedAttribute<,>).Name,     ServiceLifetime.Scoped),
        (typeof(TransientAttribute<>).Name,  typeof(TransientAttribute<,>).Name,  ServiceLifetime.Transient),
    ];

    public void RegisterFromModule<TModule>(IServiceCollection services)
    {
        HashSet<Type> visited = [];
        RegisterFromJabModuleCore(typeof(TModule), services, visited);
    }

    private static void RegisterFromJabModuleCore(Type moduleType, IServiceCollection services, HashSet<Type> visited)
    {
        if (!visited.Add(moduleType))
        {
            return;
        }

        // Process [Import<T>] attributes first (depth-first)
        foreach (CustomAttributeData attribute in moduleType.GetCustomAttributesData())
        {
            Type attributeType = attribute.AttributeType;
            if (attributeType.IsGenericType
                && attributeType.GetGenericTypeDefinition().Namespace == s_jabNamespace
                && attributeType.GetGenericTypeDefinition().Name == s_importAttribute)
            {
                Type importedModule = attributeType.GetGenericArguments()[0];
                RegisterFromJabModuleCore(importedModule, services, visited);
            }
        }

        // Process [Singleton<T>], [Scoped<T>], [Transient<T>] and their two-type-arg variants
        foreach (CustomAttributeData attribute in moduleType.GetCustomAttributesData())
        {
            Type attributeType = attribute.AttributeType;
            if (!attributeType.IsGenericType || attributeType.GetGenericTypeDefinition().Namespace != s_jabNamespace)
            {
                continue;
            }

            string attributeName = attributeType.GetGenericTypeDefinition().Name;
            string? factoryName = GetNamedStringArgument(attribute, nameof(SingletonAttribute<>.Factory));
            string? instanceName = GetNamedStringArgument(attribute, nameof(SingletonAttribute<>.Instance));
            RegistrationSource registrationSource = new(factoryName, instanceName);
            Type[] typeArgs = attributeType.GetGenericArguments();

            foreach ((string oneTArg, string twoTArgs, ServiceLifetime lifetime) in s_lifetimeMappings)
            {
                if (attributeName == oneTArg)
                {
                    RegisterWithLifetime(services, lifetime, moduleType, typeArgs[0], typeArgs[0], registrationSource);
                    break;
                }
                if (attributeName == twoTArgs)
                {
                    RegisterWithLifetime(services, lifetime, moduleType, typeArgs[0], typeArgs[1], registrationSource);
                    break;
                }
            }
        }
    }

    private static string? GetNamedStringArgument(CustomAttributeData attribute, string memberName)
    {
        foreach (CustomAttributeNamedArgument argument in attribute.NamedArguments)
        {
            if (argument.MemberName == memberName && argument.TypedValue.Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static MethodInfo GetFactoryMethod(Type moduleType, string factoryName, Type serviceType)
    {
        CacheKey cacheKey = new(moduleType, factoryName);
        if (!s_factoryMethodCache.TryGetValue(cacheKey, out MethodInfo? factoryMethod))
        {
            factoryMethod = moduleType.GetMethod(factoryName, BindingFlags.Public | BindingFlags.Static);
            // write to cache, even if negative result, to avoid repeated reflection costs for missing methods
            s_factoryMethodCache.TryAdd(cacheKey, factoryMethod);
        }
        return factoryMethod ?? throw new InvalidOperationException($"Factory method '{factoryName}' not found on type '{moduleType.FullName}' for service '{serviceType.FullName}'.");
    }

    private static PropertyInfo GetInstanceProperty(Type moduleType, string instanceName, Type serviceType)
    {
        CacheKey cacheKey = new(moduleType, instanceName);
        if (!s_instancePropertyCache.TryGetValue(cacheKey, out PropertyInfo? instanceProperty))
        {
            instanceProperty = moduleType.GetProperty(instanceName, BindingFlags.Public | BindingFlags.Static);
            // write to cache, even if negative result, to avoid repeated reflection costs for missing properties
            s_instancePropertyCache.TryAdd(cacheKey, instanceProperty);
        }
        return instanceProperty ?? throw new InvalidOperationException($"Instance property '{instanceName}' not found on type '{moduleType.FullName}' for service '{serviceType.FullName}'.");
    }

    private static void RegisterWithLifetime(IServiceCollection services, ServiceLifetime lifetime, Type moduleType, Type serviceType, Type implementationType, RegistrationSource registrationSource)
    {
        Func<IServiceProvider, object> instanceFactory = registrationSource switch
        {
            { FactoryName: { Length: > 0 } factory } => sp =>
            {
                MethodInfo factoryMethod = GetFactoryMethod(moduleType, factory, serviceType);
                ParameterInfo[] parameters = factoryMethod.GetParameters();
                object?[] args = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    args[i] = sp.GetRequiredService(parameters[i].ParameterType);
                }
                return factoryMethod.Invoke(null, args)
                    ?? throw new InvalidOperationException($"Factory method '{factory}' on '{moduleType.FullName}' returned null.");
            },
            { InstanceName: { Length: > 0 } instance } => _ => GetInstanceProperty(moduleType, instance, serviceType).GetValue(null)
                ?? throw new InvalidOperationException($"Instance property '{instance}' on '{moduleType.FullName}' returned null."),
            _ => sp => ActivatorUtilities.CreateInstance(sp, implementationType)
        };
        services.Add(new ServiceDescriptor(serviceType, instanceFactory, lifetime));
    }

    private sealed record RegistrationSource(string? FactoryName, string? InstanceName);

    private readonly record struct CacheKey(Type ModuleType, string MemberName);
}
