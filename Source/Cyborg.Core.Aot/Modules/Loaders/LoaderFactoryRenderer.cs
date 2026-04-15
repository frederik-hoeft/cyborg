using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Loaders;

internal static class LoaderFactoryRenderer
{
    private static readonly SymbolDisplayFormat s_fullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    public static string Render(LoaderGenerationModel model)
    {
        StringBuilder sourceBuilder = new(
            $$"""
            #nullable enable
            using global::Microsoft.Extensions.DependencyInjection;

            {{RenderNamespace(model.Namespace)}}
            partial class {{model.ClassSymbol.Name}}
            {
                {{model.MethodModifiers}}{{model.ModuleWorkerType.ToDisplayString(s_fullyQualifiedFormat)}} {{model.MethodName ?? "CreateWorker"}}(
                    {{model.ModuleType.ToDisplayString(s_fullyQualifiedFormat)}} module,
                    {{KnownTypes.IServiceProvider}} serviceProvider)
                {
                    return new {{model.ModuleWorkerType.ToDisplayString(s_fullyQualifiedFormat)}}(
            """);

        IndentedStringBuilder indentedBuilder = new(sourceBuilder, indentLevel: 3);
        BuildConstructorArguments(model, indentedBuilder, model.WorkerConstructor);
        sourceBuilder.AppendLine();
        sourceBuilder.Append(
            """
                    );
                }
            }
            """);

        return sourceBuilder.ToString();
    }

    private static void BuildConstructorArguments(LoaderGenerationModel model, IndentedStringBuilder builder, IMethodSymbol constructor)
    {
        for (int i = 0; i < constructor.Parameters.Length; i++)
        {
            IParameterSymbol parameter = constructor.Parameters[i];
            if (i > 0)
            {
                builder.AppendLine(",");
            }
            else
            {
                builder.Raw.AppendLine();
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, model.ModuleType))
            {
                builder.Append("module");
            }
            else if (parameter.Type is INamedTypeSymbol { IsGenericType: true, TypeArguments: [{ } typeArg] } namedType 
                && SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, model.ContractInfo.IModuleWorkerContextT)
                && SymbolEqualityComparer.Default.Equals(typeArg, model.ModuleType))
            {
                // we need to explicitly construct the worker context here
                INamedTypeSymbol boundModelContextType = model.ContractInfo.ModuleWorkerContextImplementationT.Construct(model.ModuleType);
                builder.Append($"new {boundModelContextType.ToDisplayString(s_fullyQualifiedFormat)}(");
                BuildConstructorArguments(model, builder.IncreaseIndent(), boundModelContextType.InstanceConstructors[0]);
                builder.Append(")");
            }
            else
            {
                builder.Append($"serviceProvider.GetRequiredService<{parameter.Type.ToDisplayString(s_fullyQualifiedFormat)}>()");
            }
        }
    }

    private static string RenderNamespace(string namespaceName) =>
        string.IsNullOrWhiteSpace(namespaceName)
            ? string.Empty
            : $"namespace {namespaceName};\n";
}