using System;
using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal static class ModListEditHistory
{
    // Both stacks store the next command at the end. Remove whole commands,
    // never individual snapshots: a move between two lists must stay atomic.
    // Once a command is removed, later replay of an older/full-list snapshot
    // for any of its instances would cross that missing history boundary.
    // Propagate only in replay order, retaining independent commands and newer
    // Undo steps whose snapshots are still valid on the current list state.
    internal static void InvalidateInstance<T>(List<T> history, string folderName,
        Func<T, IReadOnlyList<InstanceSnapshot>> snapshots)
    {
        var invalidated = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { folderName };
        for (var index = history.Count - 1; index >= 0; index--)
        {
            var instances = snapshots(history[index]);
            if (!instances.Any(snapshot => invalidated.Contains(snapshot.FolderName))) continue;
            invalidated.UnionWith(instances.Select(snapshot => snapshot.FolderName));
            history.RemoveAt(index);
        }
    }
}
