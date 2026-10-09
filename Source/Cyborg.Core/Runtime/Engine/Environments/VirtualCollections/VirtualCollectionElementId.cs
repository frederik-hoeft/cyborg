using System.Globalization;

namespace Cyborg.Core.Runtime.Engine.Environments.VirtualCollections;

/// <summary>
/// Allocates process-local monotonically increasing identities for virtual-collection elements.
/// </summary>
internal static class VirtualCollectionElementId
{
    private static long s_nextId;

    public static string Allocate()
    {
        long id = Interlocked.Increment(ref s_nextId);
        if (id <= 0)
        {
            throw new InvalidOperationException("Virtual-collection element identity allocation was exhausted.");
        }
        return id.ToString("D20", CultureInfo.InvariantCulture);
    }
}
