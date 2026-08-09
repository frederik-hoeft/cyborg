using Jab;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Cyborg.Core.TestAdapter;

internal sealed class JabServiceDiscovery : IJabServiceDiscovery
{
    private const BindingFlags ANY_STATIC = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    // caches for reflection-based lookups to avoid repeated reflection costs
    private static readonly ConcurrentDictionary<CacheKey, PropertyInfo?> s_instancePropertyCache = [];
    private static readonly ConcurrentDictionary<CacheKey, MethodInfo?> s_factoryMethodCache = [];
    // Names and namespace derived from typeof so they track Jab's actual declarations.
    private static readonly string s_jabNamespace = typeof(ImportAttribute<>).Namespace!;
    private static readonly string s_importAttribute = typeof(ImportAttribute<>).Name;
    private static readonly ConstructorInfo s_invalidOperationExceptionCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])
        ?? throw new InvalidOperationException("Could not find constructor for InvalidOperationException(string).");

    private readonly HashSet<Type> _visitedModules = [];

    // must match by name, since Jab generates internal attribute types per assembly, so the actual Type objects are different
    private static readonly (string OneTArg, string TwoTArgs, ServiceLifetime Lifetime)[] s_lifetimeMappings =
    [
        (typeof(SingletonAttribute<>).Name,  typeof(SingletonAttribute<,>).Name,  ServiceLifetime.Singleton),
        (typeof(ScopedAttribute<>).Name,     typeof(ScopedAttribute<,>).Name,     ServiceLifetime.Scoped),
        (typeof(TransientAttribute<>).Name,  typeof(TransientAttribute<,>).Name,  ServiceLifetime.Transient),
    ];

    public void RegisterFromModule<TModule>(IServiceCollection services) =>
        RegisterFromJabModuleCore(typeof(TModule), services, _visitedModules);

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
            factoryMethod = moduleType.GetMethod(factoryName, ANY_STATIC);
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
            instanceProperty = moduleType.GetProperty(instanceName, ANY_STATIC);
            // write to cache, even if negative result, to avoid repeated reflection costs for missing properties
            s_instancePropertyCache.TryAdd(cacheKey, instanceProperty);
        }
        return instanceProperty ?? throw new InvalidOperationException($"Instance property '{instanceName}' not found on type '{moduleType.FullName}' for service '{serviceType.FullName}'.");
    }

    private static ServiceDescriptor CreateFactoryDescriptor(ServiceLifetime lifetime, Type moduleType, Type serviceType, string factoryName)
    {
        MethodInfo factoryMethod = GetFactoryMethod(moduleType, factoryName, serviceType);

        if (!serviceType.IsAssignableFrom(factoryMethod.ReturnType))
        {
            throw new InvalidOperationException($"Factory method '{factoryName}' on '{moduleType.FullName}' returns '{factoryMethod.ReturnType.FullName}', " +
                $"which is not assignable to service type '{serviceType.FullName}'.");
        }

        ParameterInfo[] parameters = factoryMethod.GetParameters();
        Func<IServiceProvider, object> factory = CompileFactory(moduleType, serviceType, factoryMethod, parameters);

        return new ServiceDescriptor(serviceType, factory, lifetime);
    }

    private static Func<IServiceProvider, object> CompileFactory(Type moduleType, Type serviceType, MethodInfo factoryMethod, ParameterInfo[] parameters)
    {
        ParameterExpression serviceProvider = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");

        // sp => factoryMethod(sp.GetRequiredService<param1>(), sp.GetService<param2>() ?? defaultValue, ...) as serviceType ?? throw new InvalidOperationException(...)
        return Expression.Lambda<Func<IServiceProvider, object>>(
            Expression.Coalesce(
                Expression.Convert(
                    Expression.Call(
                        factoryMethod,
                        parameters.Select<ParameterInfo, Expression>(p => p.HasDefaultValue
                            ? Expression.Convert(
                                Expression.Coalesce(
                                    Expression.Call(
                                        serviceProvider,
                                        typeof(IServiceProvider).GetMethod(nameof(IServiceProvider.GetService))!,
                                        Expression.Constant(p.ParameterType)),
                                    Expression.Convert(
                                        Expression.Constant(p.DefaultValue, p.ParameterType),
                                        typeof(object))),
                                p.ParameterType)
                            : Expression.Call(
                                type: typeof(ServiceProviderServiceExtensions),
                                methodName: nameof(ServiceProviderServiceExtensions.GetRequiredService),
                                typeArguments: [p.ParameterType],
                                arguments: serviceProvider))),
                    typeof(object)),
                Expression.Throw(
                    Expression.New(
                        s_invalidOperationExceptionCtor,
                        Expression.Constant(
                            $"Factory method '{factoryMethod.Name}' on '{moduleType.FullName}' returned null for service '{serviceType.FullName}'.")),
                    typeof(object))),
            serviceProvider
        ).Compile();
    }

    private static ServiceDescriptor CreateInstanceDescriptor(ServiceLifetime lifetime, Type moduleType, Type serviceType, string instanceName)
    {
        if (lifetime is not ServiceLifetime.Singleton)
        {
            throw new InvalidOperationException($"Instance registration '{instanceName}' for service '{serviceType.FullName}' must have singleton lifetime.");
        }

        PropertyInfo instanceProperty = GetInstanceProperty(moduleType, instanceName, serviceType);

        if (!serviceType.IsAssignableFrom(instanceProperty.PropertyType))
        {
            throw new InvalidOperationException($"Instance property '{instanceName}' on '{moduleType.FullName}' has type '{instanceProperty.PropertyType.FullName}', " +
                $"which is not assignable to service type '{serviceType.FullName}'.");
        }

        object instance = instanceProperty.GetValue(null)
            ?? throw new InvalidOperationException($"Instance property '{instanceName}' on '{moduleType.FullName}' returned null.");

        return new ServiceDescriptor(serviceType, instance);
    }

    private static void RegisterWithLifetime(IServiceCollection services, ServiceLifetime lifetime, Type moduleType, Type serviceType, Type implementationType, RegistrationSource registrationSource)
    {
        if (registrationSource is { FactoryName.Length: > 0, InstanceName.Length: > 0 })
        {
            throw new InvalidOperationException($"Service registration for '{serviceType.FullName}' on module '{moduleType.FullName}' specifies both a factory and an instance.");
        }

        ServiceDescriptor descriptor = registrationSource switch
        {
            { FactoryName: { Length: > 0 } factoryName } => CreateFactoryDescriptor(lifetime, moduleType, serviceType, factoryName),
            { InstanceName: { Length: > 0 } instanceName } => CreateInstanceDescriptor(lifetime, moduleType, serviceType, instanceName),
            _ => new ServiceDescriptor(serviceType, implementationType, lifetime),
        };

        services.Add(descriptor);
    }

    private sealed record RegistrationSource(string? FactoryName, string? InstanceName);

    private readonly record struct CacheKey(Type ModuleType, string MemberName);
}
