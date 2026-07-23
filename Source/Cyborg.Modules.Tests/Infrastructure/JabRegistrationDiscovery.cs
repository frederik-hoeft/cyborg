using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Cyborg.Modules.Tests.Infrastructure;

/// <summary>
/// Discovers Jab <c>SingletonAttribute&lt;TService&gt;</c> and <c>SingletonAttribute&lt;TService, TImplementation&gt;</c>
/// declarations on <c>[ServiceProviderModule]</c>-annotated interfaces and translates them into
/// <see cref="IServiceCollection"/> registrations via reflection.
/// </summary>
/// <remarks>
/// <para>
/// Jab generates its attribute types as <c>internal</c> within each project that references the Jab package,
/// so we cannot use <c>typeof(SingletonAttribute&lt;&gt;)</c> directly from the test project. Instead, this
/// class uses <see cref="CustomAttributeData"/> and matches attribute types by their unbound generic name
/// (e.g., <c>"SingletonAttribute`1"</c>, <c>"ImportAttribute`1"</c>) in the <c>Jab</c> namespace.
/// </para>
/// <para>
/// This class intentionally uses reflection, which is acceptable in the test project since AOT compilation
/// is not a constraint. It bridges the gap between Jab's compile-time DI model and MEDI's runtime model,
/// enabling the test infrastructure to replicate the same service graph that the production CLI composes
/// via Jab source generation.
/// </para>
/// </remarks>
internal static class JabRegistrationDiscovery
{
    // TODO: support additional attribute types (e.g., [Scoped<T>], [Transient<T>])
    private const string JAB_NAMESPACE = "Jab";
    private const string SINGLETON_ATTRIBUTE_1 = "SingletonAttribute`1";
    private const string SINGLETON_ATTRIBUTE_2 = "SingletonAttribute`2";
    private const string IMPORT_ATTRIBUTE_1 = "ImportAttribute`1";

    /// <summary>
    /// Scans the specified Jab service provider module interface for singleton attributes
    /// (including those from <c>[Import&lt;TModule&gt;]</c>-referenced modules) and registers each discovered
    /// service as a singleton in the given <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// Factory-based singletons (those with <c>Factory</c> set) invoke the static factory method on the declaring
    /// interface. Constructor-based singletons use <see cref="ActivatorUtilities"/> for instantiation.
    /// <c>[Import&lt;TModule&gt;]</c>-referenced modules are recursively processed.
    /// </remarks>
    /// <typeparam name="TModule">The Jab service provider module interface type to scan.</typeparam>
    /// <param name="services">The service collection to populate with discovered registrations.</param>
    public static void RegisterFromJabModule<TModule>(IServiceCollection services)
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
            if (IsJabAttribute(attributeType, IMPORT_ATTRIBUTE_1))
            {
                Type importedModule = attributeType.GetGenericArguments()[0];
                RegisterFromJabModuleCore(importedModule, services, visited);
            }
        }

        // Process [Singleton<T>] and [Singleton<T, TImpl>] attributes
        foreach (CustomAttributeData attribute in moduleType.GetCustomAttributesData())
        {
            Type attributeType = attribute.AttributeType;
            Type[] typeArgs = attributeType.IsGenericType ? attributeType.GetGenericArguments() : [];
            string? factoryName = GetFactoryName(attribute);

            if (IsJabAttribute(attributeType, SINGLETON_ATTRIBUTE_1))
            {
                Type serviceType = typeArgs[0];
                if (factoryName is not null)
                {
                    RegisterFactory(services, moduleType, serviceType, factoryName);
                }
                else
                {
                    services.AddSingleton(serviceType, serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, serviceType));
                }
            }
            else if (IsJabAttribute(attributeType, SINGLETON_ATTRIBUTE_2))
            {
                Type serviceType = typeArgs[0];
                Type implementationType = typeArgs[1];
                if (factoryName is not null)
                {
                    RegisterFactory(services, moduleType, serviceType, factoryName);
                }
                else
                {
                    services.AddSingleton(serviceType, serviceProvider => ActivatorUtilities.CreateInstance(serviceProvider, implementationType));
                }
            }
        }
    }

    private static bool IsJabAttribute(Type attributeType, string expectedUnboundName)
    {
        if (!attributeType.IsGenericType)
        {
            return false;
        }
        Type genericDefinition = attributeType.GetGenericTypeDefinition();
        return genericDefinition.Namespace == JAB_NAMESPACE && genericDefinition.Name == expectedUnboundName;
    }

    private static string? GetFactoryName(CustomAttributeData attribute)
    {
        foreach (CustomAttributeNamedArgument namedArg in attribute.NamedArguments)
        {
            if (namedArg.MemberName == "Factory" && namedArg.TypedValue.Value is string factoryName)
            {
                return factoryName;
            }
        }
        return null;
    }

    private static void RegisterFactory(IServiceCollection services, Type moduleType, Type serviceType, string factoryName)
    {
        MethodInfo? factoryMethod = moduleType.GetMethod(factoryName, BindingFlags.Public | BindingFlags.Static);
        if (factoryMethod is null)
        {
            throw new InvalidOperationException(
                $"Factory method '{factoryName}' not found on type '{moduleType.FullName}' for service '{serviceType.FullName}'.");
        }

        services.AddSingleton(serviceType, serviceProvider =>
        {
            ParameterInfo[] parameters = factoryMethod.GetParameters();
            object?[] args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = serviceProvider.GetRequiredService(parameters[i].ParameterType);
            }
            return factoryMethod.Invoke(null, args)
                ?? throw new InvalidOperationException($"Factory method '{factoryName}' on '{moduleType.FullName}' returned null.");
        });
    }
}