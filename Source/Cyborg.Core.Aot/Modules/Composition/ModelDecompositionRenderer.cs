using Cyborg.Core.Aot.Extensions;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace Cyborg.Core.Aot.Modules.Composition;

internal static class ModelDecompositionRenderer
{
    private const string DECOMPOSE_METHOD_NAME = "Decompose";
    private const string COMPOSE_METHOD_NAME = "Compose";

    private static readonly SymbolDisplayFormat s_fullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Included);

    public static string Render(DecompositionGenerationModel model, DecompositionContractInfo contractInfo)
    {
        string iDecomposable = contractInfo.IDecomposable.ToDisplayString(s_fullyQualifiedFormat);
        string dynamicKvp = contractInfo.DynamicKeyValuePair.ToDisplayString(s_fullyQualifiedFormat);
        string storeType = contractInfo.IHierarchicalKeyValueStore.ToDisplayString(s_fullyQualifiedFormat);
        string iDecomposableOfSelf = $"{iDecomposable}<{model.TypeSymbol.Name}>";

        StringBuilder sourceBuilder = new();
        sourceBuilder.Append(
            $$"""
            #nullable enable

            {{RenderNamespace(model.Namespace)}}
            partial {{model.TypeKeyword}} {{model.TypeSymbol.Name}} : {{iDecomposableOfSelf}}
            {
            """);

        IndentedStringBuilder body = new(sourceBuilder, indentLevel: 1);
        body.AppendBlock(
            $$"""
            public {{KnownTypes.IEnumerableOfT(dynamicKvp)}} {{DECOMPOSE_METHOD_NAME}}() =>
            [
            """);

        IndentedStringBuilder decomposeItems = body.IncreaseIndent();
        foreach (DecompositionPropertyModel property in model.DecomposableProperties)
        {
            decomposeItems.AppendLine($"new({property.ConvertedKeyExpression}, {property.Property.Name}),");
        }

        body.AppendBlock(
            $$"""
            ];

            public static {{model.TypeSymbol.Name}} {{COMPOSE_METHOD_NAME}}({{storeType}} store, string rootPath)
            {
            #pragma warning disable CS8600 // Compose falls back to default for missing leaves; nullability is enforced by publishers/callers.
                {{KnownTypes.ArgumentNullException}}.ThrowIfNull(store);
                global::System.ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            """);

        IndentedStringBuilder composeBody = body.IncreaseIndent();
        foreach (DecompositionPropertyModel property in model.DecomposableProperties)
        {
            string localName = SymbolNameGenerator.MakeCamelCase(property.Property.Name);
            // Avoid keyword collisions for common parameter-like names
            if (SyntaxFactsContains(localName))
            {
                localName = "@" + localName;
            }

            string pathLocal = $"{localName}Path";
            composeBody.AppendLine($"string {pathLocal} = global::Cyborg.Core.Configuration.HierarchicalKeyValueStoreExtensions.CombinePath(rootPath, {property.ConvertedKeyExpression});");

            if (property.IsComposable)
            {
                if (property.IsNullable)
                {
                    composeBody.AppendBlock(
                        $$"""
                        {{property.PropertyTypeDisplayName}} {{localName}} = default;
                        if (global::Cyborg.Core.Configuration.HierarchicalKeyValueStoreExtensions.HasValues(store, {{pathLocal}}))
                        {
                            {{localName}} = {{property.ComposableTypeDisplayName}}.{{COMPOSE_METHOD_NAME}}(store, {{pathLocal}});
                        }
                        """);
                }
                else
                {
                    composeBody.AppendLine($"{property.PropertyTypeDisplayName} {localName} = {property.ComposableTypeDisplayName}.{COMPOSE_METHOD_NAME}(store, {pathLocal});");
                }
            }
            else
            {
                // Out variable type must be the non-nullable T: for unconstrained generics, `T?` on
                // TryGetValue is a nullability annotation, not Nullable<T>. Use a discard-default
                // fallback so missing leaves compose to the type's default without CS8600 noise.
                composeBody.AppendLine(
                    $"{property.PropertyTypeDisplayName} {localName} = store.TryGetValue<{property.NonNullableTypeDisplayName}>({pathLocal}, out {property.NonNullableTypeDisplayName} {localName}Value) ? {localName}Value : default!;");
            }
        }

        composeBody.AppendLine(RenderConstruction(model));
        body.AppendLine("#pragma warning restore CS8600");
        body.AppendLine("}");
        sourceBuilder.AppendLine("}");

        return sourceBuilder.ToString();
    }

    private static string RenderConstruction(DecompositionGenerationModel model)
    {
        ImmutableArray<IParameterSymbol> ctorParams = model.PrimaryConstructorParameters;
        HashSet<string> ctorParamNames = new(ctorParams.Select(static p => p.Name), StringComparer.Ordinal);

        // Map constructor parameters to property locals by name (primary constructor / matching ctor).
        List<string> ctorArgs = [];
        foreach (IParameterSymbol parameter in ctorParams)
        {
            DecompositionPropertyModel? matchingProperty = model.DecomposableProperties
                .FirstOrDefault(p => string.Equals(p.Property.Name, parameter.Name, StringComparison.Ordinal)
                    || string.Equals(SymbolNameGenerator.MakeCamelCase(p.Property.Name), parameter.Name, StringComparison.Ordinal));

            if (matchingProperty is null)
            {
                // Parameter is not a decomposable property; use default
                ctorArgs.Add($"{parameter.Name}: default!");
                continue;
            }

            string localName = SymbolNameGenerator.MakeCamelCase(matchingProperty.Property.Name);
            if (SyntaxFactsContains(localName))
            {
                localName = "@" + localName;
            }
            ctorArgs.Add($"{parameter.Name}: {localName}");
        }

        List<DecompositionPropertyModel> remainingProperties = [.. model.DecomposableProperties
            .Where(p => !ctorParamNames.Contains(p.Property.Name)
                && !ctorParamNames.Contains(SymbolNameGenerator.MakeCamelCase(p.Property.Name)))];

        string ctorCall = ctorArgs.Count > 0
            ? $"new {model.TypeSymbol.Name}({string.Join(", ", ctorArgs)})"
            : $"new {model.TypeSymbol.Name}()";

        if (remainingProperties.Count == 0)
        {
            return $"return {ctorCall};";
        }

        if (model.TypeSymbol.IsRecord)
        {
            string withAssignments = string.Join(", ", remainingProperties.Select(p =>
            {
                string localName = SymbolNameGenerator.MakeCamelCase(p.Property.Name);
                if (SyntaxFactsContains(localName))
                {
                    localName = "@" + localName;
                }
                return $"{p.Property.Name} = {localName}";
            }));
            return $"return {ctorCall} with {{ {withAssignments} }};";
        }

        string initializer = string.Join(", ", remainingProperties.Select(p =>
        {
            string localName = SymbolNameGenerator.MakeCamelCase(p.Property.Name);
            if (SyntaxFactsContains(localName))
            {
                localName = "@" + localName;
            }
            return $"{p.Property.Name} = {localName}";
        }));
        return $"return {ctorCall} {{ {initializer} }};";
    }

    private static string RenderNamespace(string namespaceName) =>
        string.IsNullOrWhiteSpace(namespaceName)
            ? string.Empty
            : $"""
              namespace {namespaceName};


              """;

    private static bool SyntaxFactsContains(string identifier) =>
        identifier is "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or "char"
            or "checked" or "class" or "const" or "continue" or "decimal" or "default" or "delegate" or "do"
            or "double" or "else" or "enum" or "event" or "explicit" or "extern" or "false" or "finally" or "fixed"
            or "float" or "for" or "foreach" or "goto" or "if" or "implicit" or "in" or "int" or "interface"
            or "internal" or "is" or "lock" or "long" or "namespace" or "new" or "null" or "object" or "operator"
            or "out" or "override" or "params" or "private" or "protected" or "public" or "readonly" or "ref"
            or "return" or "sbyte" or "sealed" or "short" or "sizeof" or "stackalloc" or "static" or "string"
            or "struct" or "switch" or "this" or "throw" or "true" or "try" or "typeof" or "uint" or "ulong"
            or "unchecked" or "unsafe" or "ushort" or "using" or "virtual" or "void" or "volatile" or "while"
            or "record" or "file" or "required" or "scoped" or "when" or "with" or "yield" or "init" or "managed"
            or "unmanaged" or "notnull" or "ninteger" or "nuint" or "nint";
}
