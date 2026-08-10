using Cyborg.Core.Configuration.Builders;
using Cyborg.Core.Modules.Debugging;
using Cyborg.Core.Modules.Debugging.Breakpoints;
using Cyborg.Core.Services.Default;
using System.Diagnostics.CodeAnalysis;

namespace Cyborg.Cli.Debugging;

internal sealed class CliDebugArgumentHandler(IBreakpointRegistry breakpoints, IDefault<IDebugFrontend> defaultFrontend) : ICliDebugArgumentHandler
{
    private const string CONSOLE_FRONTEND_KEY = "console";

    public string FrontendConfigurationKey => defaultFrontend.ConfigurationKey;

    public bool TryConfigure(IConfigurationBuilder configurationBuilder, string[]? breakAt, [NotNullWhen(false)] out string? invalidExpression,
        [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        invalidExpression = null;
        errorMessage = null;
        if (breakAt is not { Length: > 0 })
        {
            return true;
        }

        List<int> addedBreakpoints = [];
        foreach (string expression in breakAt)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                continue;
            }

            try
            {
                addedBreakpoints.Add(breakpoints.Add(expression));
            }
            catch (ArgumentException exception)
            {
                // RegexParseException derives from ArgumentException.
                foreach (int breakpointId in addedBreakpoints)
                {
                    breakpoints.Remove(breakpointId);
                }
                invalidExpression = expression;
                errorMessage = exception.Message;
                return false;
            }
        }

        if (addedBreakpoints.Count > 0)
        {
            // CLI debugging has a console frontend, so provide it as a low-precedence host default. Configuration files added after this source may override it.
            configurationBuilder.AddDictionary(dictionary => dictionary.AddEntry(FrontendConfigurationKey, CONSOLE_FRONTEND_KEY));
        }
        return true;
    }

    public bool HasUsableFrontend() => breakpoints.Count == 0 || defaultFrontend.GetDefault() is not null;
}
