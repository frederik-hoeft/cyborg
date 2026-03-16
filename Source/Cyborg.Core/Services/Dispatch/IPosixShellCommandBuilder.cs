namespace Cyborg.Core.Services.Dispatch;

public interface IPosixShellCommandBuilder
{
    string QuoteArgument(string argument);

    string BuildCommand(IReadOnlyList<string> arguments);

    string BuildCommand(string executable, IReadOnlyList<string> arguments);
}