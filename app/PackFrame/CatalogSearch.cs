using System;
using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal static class CatalogSearch
{
    internal static List<ModpackListItem> Filter(IEnumerable<ModpackListItem> items, string? query)
    {
        var term = query?.Trim() ?? string.Empty;
        return items.Where(item => term.Length == 0 ||
            item.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            item.Summary.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            (item.Entry.Id?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
    }

    // Match the existing catalog identity, never a possibly duplicated display name.
    internal static int Find(IReadOnlyList<ModpackListItem> items, ModpackEntry? entry)
    {
        if (entry is null) return -1;
        for (var i = 0; i < items.Count; i++)
            if (!string.IsNullOrWhiteSpace(entry.Id)
                ? items[i].Entry.Id == entry.Id
                : items[i].Entry.File == entry.File && items[i].Entry.GiteeFile == entry.GiteeFile)
                return i;
        return -1;
    }
}
