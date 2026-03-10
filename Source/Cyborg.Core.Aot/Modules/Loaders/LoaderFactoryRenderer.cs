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
        for (int i = 0; i < model.WorkerConstructor.Parameters.Length; i++)
        {
            IParameterSymbol parameter = model.WorkerConstructor.Parameters[i];
            if (i > 0)
            {
                sourceBuilder.AppendLine(",");
            }
            else
            {
                sourceBuilder.AppendLine();
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, model.ModuleType))
            {
                indentedBuilder.Append("module");
            }
            else
            {
                indentedBuilder.Append($"serviceProvider.GetRequiredService<{parameter.Type.ToDisplayString(s_fullyQualifiedFormat)}>()");
            }
        }
        sourceBuilder.AppendLine();
        sourceBuilder.Append(
            """
                    );
                }
            }
            """);

        return sourceBuilder.ToString();
    }

    private static string RenderNamespace(string namespaceName) =>
        string.IsNullOrWhiteSpace(namespaceName)
            ? string.Empty
            : $"namespace {namespaceName};\n";
}