using Jab;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Cyborg.Core.TestAdapter;

public sealed class JabServiceDiscovery : IJabServiceDiscovery
{
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
            string? factoryName = GetFactoryName(attribute);
            Type[] typeArgs = attributeType.GetGenericArguments();

            foreach ((string oneTArg, string twoTArgs, ServiceLifetime lifetime) in s_lifetimeMappings)
            {
                if (attributeName == oneTArg)
                {
                    RegisterWithLifetime(services, lifetime, moduleType, typeArgs[0], typeArgs[0], factoryName);
                    break;
                }
                if (attributeName == twoTArgs)
                {
                    RegisterWithLifetime(services, lifetime, moduleType, typeArgs[0], typeArgs[1], factoryName);
                    break;
                }
            }
        }
    }

    private static string? GetFactoryName(CustomAttributeData attribute)
    {
        foreach (CustomAttributeNamedArgument namedArg in attribute.NamedArguments)
        {
            // factory name should be the same for all attribute types
            if (namedArg.MemberName == nameof(SingletonAttribute<>.Factory) && namedArg.TypedValue.Value is string factoryName)
            {
                return factoryName;
            }
        }
        return null;
    }

    private static void RegisterWithLifetime(IServiceCollection services, ServiceLifetime lifetime, Type moduleType, Type serviceType, Type implementationType, string? factoryName)
    {
        Func<IServiceProvider, object> factory;
        if (factoryName is not null)
        {
            MethodInfo factoryMethod = moduleType.GetMethod(factoryName, BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Factory method '{factoryName}' not found on type '{moduleType.FullName}' for service '{serviceType.FullName}'.");
            factory = sp =>
            {
                ParameterInfo[] parameters = factoryMethod.GetParameters();
                object?[] args = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = sp.GetRequiredService(parameters[i].ParameterType);
                }
                return factoryMethod.Invoke(null, args)
                    ?? throw new InvalidOperationException($"Factory method '{factoryName}' on '{moduleType.FullName}' returned null.");
            };
        }
        else
        {
            factory = sp => ActivatorUtilities.CreateInstance(sp, implementationType);
        }
        services.Add(new ServiceDescriptor(serviceType, factory, lifetime));
    }
}
