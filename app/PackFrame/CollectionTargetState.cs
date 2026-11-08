using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PackFrame;

// A matching title is not an identity. Updating an existing collection always
// requires an explicit visible selection; filtering must never pick another ID.
internal sealed class CollectionTargetState
{
    private readonly IReadOnlyList<OwnedCollection> _candidates;
    private readonly ulong[]? _desired;
    internal CollectionTargetState(IEnumerable<OwnedCollection> candidates, IEnumerable<ulong>? desired)
    {
        _candidates = candidates.Where(c => c.Id != 0).DistinctBy(c => c.Id)
            .Select(c => c with { Children = c.Children.Distinct().ToArray() }).ToArray();
        _desired = desired?.Distinct().ToArray();
        Visible = _candidates;
    }
    internal int TotalCount => _candidates.Count;
    internal IReadOnlyList<OwnedCollection> Visible { get; private set; }
    internal OwnedCollection? Selected { get; private set; }
    internal void Filter(string? query)
    {
        var term = query?.Trim() ?? string.Empty;
        Visible = _candidates.Where(c => term.Length == 0 ||
            c.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            c.Id.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)).ToArray();
        if (Selected is not null && !Visible.Any(c => c.Id == Selected.Id)) Selected = null;
    }
    internal void Select(ulong? id) => Selected = Visible.FirstOrDefault(c => c.Id == id);
    internal (int Add, int Remove)? Changes
    {
        get
        {
            if (Selected is null || _desired is null) return null;
            var delta = SteamCollectionClient.CollectionDelta(Selected.Children, _desired);
            return (delta.Add.Count, delta.Remove.Count);
        }
    }
}
