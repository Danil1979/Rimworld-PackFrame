using Avalonia.Controls;
using PackFrame;

internal static class PaneSelectionChecks
{
    public static void Run()
    {
        var alpha = Mod("alpha");
        var beta = Mod("beta");
        var gamma = Mod("gamma");
        var keyboard = Pane(alpha, beta, gamma);
        keyboard.SelectedItems!.Add(alpha);
        var endpoint = PaneSelectionRange.Move(keyboard, 1, extend: true, anchorIndex: 0, endpointIndex: 0);
        Require(Selected(keyboard).SequenceEqual(new[] { "alpha", "beta" }),
            "keyboard range selection includes both the anchor and range endpoint");
        endpoint = PaneSelectionRange.Move(keyboard, 1, extend: true, anchorIndex: 0, endpointIndex: endpoint);
        Require(Selected(keyboard).SequenceEqual(new[] { "alpha", "beta", "gamma" }) && endpoint == 2,
            "repeated Shift+Down extends from the range endpoint rather than the first selected row");
        var otherKeyboard = Pane(alpha, beta, gamma);
        otherKeyboard.SelectedItems!.Add(gamma);
        var otherEndpoint = PaneSelectionRange.Move(otherKeyboard, -1, extend: true, anchorIndex: 2, endpointIndex: 2);
        endpoint = PaneSelectionRange.Move(keyboard, -1, extend: true, anchorIndex: 0, endpointIndex: endpoint);
        Require(Selected(keyboard).SequenceEqual(new[] { "alpha", "beta" }) &&
                Selected(otherKeyboard).SequenceEqual(new[] { "beta", "gamma" }) && otherEndpoint == 1,
            "returning to a pane resumes its own keyboard range endpoint without changing the other pane");
        endpoint = PaneSelectionRange.Move(keyboard, -1, extend: true, anchorIndex: 0, endpointIndex: endpoint);
        Require(Selected(keyboard).SequenceEqual(new[] { "alpha" }) && endpoint == 0,
            "repeated Shift+Up shrinks the range back to its anchor");
        PaneSelectionRange.Select(keyboard, 2, 0);
        Require(Selected(keyboard).SequenceEqual(new[] { "alpha", "beta", "gamma" }),
            "pointer Shift selection uses the same inclusive range model in reverse order");
        keyboard.SelectedItems.Clear();
        keyboard.SelectedItems.Add(alpha);
        PaneSelectionRange.Select(keyboard, 1, 2, additive: true);
        Require(Selected(keyboard).SequenceEqual(new[] { "alpha", "beta", "gamma" }),
            "Ctrl+Shift pointer ranges preserve the existing selection");
        var source = Pane(alpha, beta, gamma);
        source.SelectedItems!.Add(alpha);
        source.SelectedItems.Add(beta);
        var destination = Pane(alpha with { }, beta with { }, gamma with { });
        destination.SelectedItem = destination.Items[0];
        var destinationState = PaneSelectionSnapshot.Capture(destination, "instance-A", "alpha");
        destinationState.Restore(destination, new[] { gamma with { } }, "instance-A", selectFirst: true);
        Require(Selected(source).SequenceEqual(new[] { "alpha", "beta" }) && destination.SelectedItems!.Count == 0,
            "searching the destination preserves the source multi-selection and clears hidden destination rows");

        var sourceState = PaneSelectionSnapshot.Capture(source, "instance-A", "beta");
        var rebuilt = new[] { gamma with { }, beta with { PackageId = "BETA" }, alpha with { } };
        var anchor = sourceState.Restore(source, rebuilt, "instance-A", selectFirst: false);
        Require(Selected(source).SequenceEqual(new[] { "BETA", "alpha" }) && anchor == 1 &&
                source.SelectedItems!.Cast<ModInfo>().All(rebuilt.Contains),
            "rebuilding a pane restores its visible multi-selection and Shift anchor onto the new row models");

        var reloadState = PaneSelectionSnapshot.Capture(source, "instance-A", "BETA");
        source.ItemsSource = null; // Full catalog reload clears controls before rows return.
        var reloaded = new[] { alpha with { }, beta with { }, gamma with { } };
        var reloadAnchor = reloadState.Restore(source, reloaded, "instance-A", selectFirst: false);
        Require(Selected(source).SequenceEqual(new[] { "alpha", "beta" }) && reloadAnchor == 1,
            "snapshot survives catalog reset and restores multi-selection and anchor");
        Require(ReferenceEquals(PaneSelectionSnapshot.ResolveDetails(source, "BETA"), reloaded[1]),
            "refresh resolves last inspected selected mod onto its newly loaded source");
        reloadState.Restore(source, new[] { gamma with { } }, "instance-A", selectFirst: false);
        Require(source.SelectedItems!.Count == 0 && PaneSelectionSnapshot.ResolveDetails(source, "beta") is null,
            "refresh clears vanished selections rather than choosing an unrelated mod");
        sourceState.Restore(source, rebuilt, "instance-A", selectFirst: false);

        var filteredState = PaneSelectionSnapshot.Capture(source, "instance-A", "BETA");
        anchor = filteredState.Restore(source, new[] { alpha with { }, gamma with { } }, "instance-A", selectFirst: true);
        Require(Selected(source).SequenceEqual(new[] { "alpha" }) && anchor == 0,
            "filtering a selected pane retains matching selections and replaces a hidden range anchor safely");
        var hiddenState = PaneSelectionSnapshot.Capture(source, "instance-A", "alpha");
        hiddenState.Restore(source, new[] { gamma with { } }, "instance-A", selectFirst: true);
        Require(source.SelectedItems!.Count == 0,
            "hiding every selected item does not silently select an unrelated mod for commands");

        var oldInstance = Pane(alpha, beta);
        oldInstance.SelectedItems!.Add(alpha);
        var oldState = PaneSelectionSnapshot.Capture(oldInstance, "instance-A", "alpha");
        anchor = oldState.Restore(oldInstance, new[] { alpha with { }, beta with { } }, "instance-B", selectFirst: false);
        Require(oldInstance.SelectedItems!.Count == 0 && anchor == -1,
            "changing a pane's instance does not carry selections to matching IDs in another instance");

        var empty = Pane();
        var emptyState = PaneSelectionSnapshot.Capture(empty, "instance-A", null);
        emptyState.Restore(empty, new[] { gamma }, "instance-A", selectFirst: true);
        Require(ReferenceEquals(empty.SelectedItem, gamma),
            "searching a previously unselected pane can select its first visible result");
        empty.AutoScrollToSelectedItem = true;
        var stable = PaneSelectionSnapshot.Capture(empty, "instance-A", "gamma");
        stable.Restore(empty, new[] { gamma with { }, alpha }, "instance-A", selectFirst: false);
        Require(empty.AutoScrollToSelectedItem,
            "selection restoration restores the pane's automatic-scroll policy after replacing rows");
    }

    private static ListBox Pane(params ModInfo[] mods) => new()
    {
        SelectionMode = SelectionMode.Multiple,
        ItemsSource = mods
    };

    private static IEnumerable<string> Selected(ListBox list) =>
        list.Items.OfType<ModInfo>().Where(mod => list.SelectedItems!.Contains(mod)).Select(mod => mod.PackageId);

    private static ModInfo Mod(string id) => new(id, "Test", id, id, string.Empty,
        null, ModSource.Local, "1.7", DateTime.MinValue);

    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
