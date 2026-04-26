using Microsoft.CodeAnalysis;

namespace Cyborg.Core.Aot.Modules.Validation;

internal sealed record DiagnosticsReporter(List<Diagnostic> Diagnostics)
{
    public void Report(DiagnosticDescriptor descriptor, Location location, params object[] messageArgs) =>
        Diagnostics.Add(Diagnostic.Create(descriptor, location, messageArgs));
}
