using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace PackFrame;

// Avalonia 12.1.4's Linux AT-SPI bridge flattens a selection control's
// descendants, but does not invalidate that flattened cache when its virtualized
// panel recycles children. Notify the list peer after realization changes.
// Keep the stock theme, selection provider, scroll provider and virtualization.
public sealed class AccessibleModListBox : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override AutomationPeer OnCreateAutomationPeer() => OperatingSystem.IsLinux()
        ? new RealizedModListAutomationPeer(this)
        : base.OnCreateAutomationPeer();
}

internal sealed class RealizedModListAutomationPeer : ListBoxAutomationPeer
{
    private Control[] _realized = [];

    internal RealizedModListAutomationPeer(ListBox owner) : base(owner)
    {
        // Run after containers have been arranged, not while ItemsSource is changing.
        // Both objects share the control's lifetime; no global event subscriptions.
        owner.LayoutUpdated += (_, _) => UpdateRealizedContainers(owner.GetRealizedContainers());
    }

    internal void UpdateRealizedContainers(IEnumerable<Control> containers)
    {
        var visible = containers.Where(control => control.IsVisible);
        // No notification or snapshot allocation for unchanged layouts.
        if (_realized.SequenceEqual(visible)) return;
        _realized = visible.ToArray();
        InvalidateChildren();
    }
}
