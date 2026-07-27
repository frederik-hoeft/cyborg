using Cyborg.Core.Modules.Configuration.Model;
using System.Collections;
using System.Globalization;
using System.Text;

namespace Cyborg.Core.Modules.Debugging;

/// <summary>
/// Runtime helpers used by source-generated <see cref="IInspectable.Inspect"/> implementations
/// and by debug frontends when formatting nested module graph values without reflection.
/// </summary>
// TODO: convert to real DI service
public static class ModuleInspection
{
    private const int INDENT_SIZE = 2;

    public static void AppendProperty(StringBuilder builder, string propertyName, object? value, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        AppendIndent(builder, indentLevel);
        builder.Append(propertyName).Append(':');
        if (value is null)
        {
            builder.AppendLine(" null");
            return;
        }

        if (IsScalar(value))
        {
            builder.Append(' ');
            AppendScalar(builder, value);
            builder.AppendLine();
            return;
        }

        builder.AppendLine();
        AppendValue(builder, value, indentLevel + 1);
    }

    public static void AppendValue(StringBuilder builder, object? value, int indentLevel)
    {
        ArgumentNullException.ThrowIfNull(builder);

        switch (value)
        {
            case null:
                AppendIndent(builder, indentLevel);
                builder.AppendLine("null");
                return;
            case IInspectable inspectable:
                AppendIndentedBlock(builder, inspectable.Inspect(), indentLevel);
                return;
            case IModuleWorker worker:
                AppendValue(builder, worker.Module, indentLevel);
                return;
            case ModuleReference reference:
                AppendValue(builder, reference.Module, indentLevel);
                return;
            case ModuleContext context:
                AppendModuleContext(builder, context, indentLevel);
                return;
            case IModule module:
                AppendIndent(builder, indentLevel);
                builder.AppendLine(module.ToString());
                return;
            case string text:
                AppendIndent(builder, indentLevel);
                AppendScalar(builder, text);
                builder.AppendLine();
                return;
            case IEnumerable enumerable:
                AppendEnumerable(builder, enumerable, indentLevel);
                return;
            default:
                AppendIndent(builder, indentLevel);
                // Records and other types expose useful default ToString output.
                builder.AppendLine(Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name);
                return;
        }
    }

    private static void AppendModuleContext(StringBuilder builder, ModuleContext context, int indentLevel)
    {
        AppendProperty(builder, "Module", context.Module, indentLevel);
        if (context.Environment is not null)
        {
            AppendProperty(builder, "Environment", context.Environment, indentLevel);
        }
        if (context.Configuration is not null)
        {
            AppendProperty(builder, "Configuration", context.Configuration, indentLevel);
        }
        if (context.Requires is not null)
        {
            AppendProperty(builder, "Requires", context.Requires, indentLevel);
        }
    }

    private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable, int indentLevel)
    {
        int index = 0;
        bool any = false;
        foreach (object? item in enumerable)
        {
            any = true;
            AppendIndent(builder, indentLevel);
            builder.Append('[').Append(index++).AppendLine("]:");
            AppendValue(builder, item, indentLevel + 1);
        }

        if (!any)
        {
            AppendIndent(builder, indentLevel);
            builder.AppendLine("(empty)");
        }
    }

    private static void AppendIndentedBlock(StringBuilder builder, string block, int indentLevel)
    {
        using StringReader reader = new(block);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            AppendIndent(builder, indentLevel);
            builder.AppendLine(line);
        }
    }

    private static void AppendIndent(StringBuilder builder, int indentLevel)
    {
        if (indentLevel > 0)
        {
            builder.Append(' ', indentLevel * INDENT_SIZE);
        }
    }

    private static bool IsScalar(object value) =>
        value is string
            or bool
            or char
            or byte or sbyte
            or short or ushort
            or int or uint
            or long or ulong
            or float or double or decimal
            or Enum
            or DateTime or DateTimeOffset or TimeSpan or Guid;

    private static void AppendScalar(StringBuilder builder, object value)
    {
        switch (value)
        {
            case string text:
                builder.Append('"').Append(text.Replace("\"", "\\\"", StringComparison.Ordinal)).Append('"');
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case Enum:
                builder.Append(value.GetType().Name).Append('.').Append(value);
                break;
            default:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
