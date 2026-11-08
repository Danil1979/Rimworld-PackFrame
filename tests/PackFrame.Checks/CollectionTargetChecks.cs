using PackFrame;

internal static class CollectionTargetChecks
{
    internal static void Run()
    {
        var children = new List<ulong> { 10, 20, 20 };
        var desired = new List<ulong> { 20, 30, 30 };
        var state = new CollectionTargetState(new[] {
            new OwnedCollection(123, "Same colony", children),
            new OwnedCollection(456, "Same colony", new ulong[] { 40 }),
            new OwnedCollection(789, "日本語の合集", Array.Empty<ulong>()),
            new OwnedCollection(123, "Duplicate record", Array.Empty<ulong>()),
            new OwnedCollection(0, "Invalid identity", Array.Empty<ulong>()) }, desired);
        Check(state.TotalCount == 3 && state.Visible.Count == 3, "collection target identities are valid and unique");
        Check(state.Selected is null && state.Changes is null, "collection target never silently selects an update destination");
        state.Select(123);
        Check(state.Selected?.Id == 123 && state.Changes == (1, 1), "collection target previews distinct additions and removals");
        children.Clear(); desired.Clear();
        Check(state.Selected?.Children.Count == 2 && state.Changes == (1, 1), "collection review snapshots its candidate and desired mod lists");
        state.Filter(" SAME ");
        Check(state.Visible.Count == 2 && state.Selected?.Id == 123, "collection title search trims and matches case-insensitively without changing identity");
        state.Filter("456");
        Check(state.Visible.Count == 1 && state.Visible[0].Id == 456 && state.Selected is null, "hiding a selected collection clears update rather than selecting another title match");
        state.Select(123);
        Check(state.Selected is null, "a hidden collection cannot be confirmed through selection state");
        state.Select(456);
        Check(state.Selected?.Id == 456 && state.Changes == (2, 1), "duplicate collection names retain independent identities and change previews");
        state.Filter("no-match");
        Check(state.Visible.Count == 0 && state.Selected is null && state.Changes is null, "no matching collections disables an existing update decision");
        state.Filter(null);
        Check(state.Visible.Count == 3 && state.Selected is null, "clearing search does not resurrect a hidden update decision");
        state.Filter("日本語");
        Check(state.Visible.Count == 1 && state.Visible[0].Id == 789, "collection names support non-Latin search");
        state.Select(789);
        Check(state.Changes == (2, 0), "empty collection preview has additions but no removals");
        state.Select(null);
        Check(state.Selected is null && state.Changes is null, "explicit deselection clears collection update preview");
        state.Select(999);
        Check(state.Selected is null, "unknown collection identity cannot be selected");
        var unavailable = new CollectionTargetState(Array.Empty<OwnedCollection>(), new ulong[] { 10 });
        unavailable.Filter("anything"); unavailable.Select(10);
        Check(unavailable.TotalCount == 0 && unavailable.Visible.Count == 0 && unavailable.Selected is null,
            "empty owned collections never enables an update target");
        var unspecified = new CollectionTargetState(new[] { new OwnedCollection(1, "One", new ulong[] { 10 }) }, null);
        unspecified.Select(1);
        Check(unspecified.Selected?.Id == 1 && unspecified.Changes is null, "unspecified desired mods do not claim a removal preview");
    }
    private static void Check(bool passed, string name)
    {
        if (!passed) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
