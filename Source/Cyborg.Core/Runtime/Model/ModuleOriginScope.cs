namespace Cyborg.Core.Runtime.Model;

/// <summary>
/// Async-local definition site for the configuration file currently being deserialized.
/// Nested loads restore the previous origin when disposed.
/// </summary>
internal static class ModuleOriginScope
{
    private static readonly AsyncLocal<Scope?> s_current = new();

    public static ModuleOrigin? Current => s_current.Value?.Origin;

    public static IDisposable Push(ModuleOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        Scope scope = new(origin, s_current.Value);
        s_current.Value = scope;
        return scope;
    }

    private sealed class Scope(ModuleOrigin origin, Scope? previous) : IDisposable
    {
        private bool _disposed;

        public ModuleOrigin Origin { get; } = origin;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            s_current.Value = previous;
        }
    }
}
