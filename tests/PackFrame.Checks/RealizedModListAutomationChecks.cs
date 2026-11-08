using System.Reflection;
using System.Xml.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using PackFrame;

internal static class RealizedModListAutomationChecks
{
    internal static void Run()
    {
        var list = new AccessibleModListBox();
        var peer = new RealizedModListAutomationPeer(list);
        var notifications = 0;
        peer.ChildrenChanged += (_, _) => notifications++;
        var a = new ListBoxItem(); var b = new ListBoxItem(); var c = new ListBoxItem();
        peer.UpdateRealizedContainers([a, b, c]);
        Require(notifications == 1, "realization notifies the list's flattened accessibility cache");
        peer.UpdateRealizedContainers([a, b, c]);
        Require(notifications == 1, "unchanged layout does not emit repeated accessibility invalidations");
        a.IsVisible = b.IsVisible = false;
        peer.UpdateRealizedContainers([a, b, c]);
        Require(notifications == 2, "filtering hides recycled containers from the accessibility snapshot");
        c.IsVisible = false;
        peer.UpdateRealizedContainers([a, b, c]);
        Require(notifications == 3, "zero matches invalidates stale accessible children");
        peer.UpdateRealizedContainers([]);
        Require(notifications == 3, "hidden recycle-pool changes do not invalidate an already empty snapshot");
        a.IsVisible = b.IsVisible = c.IsVisible = true;
        peer.UpdateRealizedContainers([a, b, c]);
        peer.UpdateRealizedContainers([c, a, b]);
        Require(notifications == 5, "restore and changed realization order each invalidate the accessibility cache");
        Require(peer.GetProvider<ISelectionProvider>() is not null && peer.GetProvider<IScrollProvider>() is not null,
            "realization fix preserves standard selection and scroll automation providers");
        Require(list.StyleKey == typeof(ListBox), "specialized mod list retains the stock ListBox theme");
        Require(ControlAutomationPeer.CreatePeerForElement(list) is RealizedModListAutomationPeer == OperatingSystem.IsLinux(),
            "platform workaround is limited to the Linux AT-SPI bridge");
        foreach(var window in new[]{"MainWindow", "MissingModsWindow", "MissingHistoryWindow"})
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UiWiring." + window + ".axaml")!;
            var document = XDocument.Load(stream);
            var lists = document.Descendants().Where(e => e.Name.LocalName == nameof(AccessibleModListBox)).ToArray();
            Require(lists.Length == (window == "MainWindow" ? 3 : 1), window + " uses accessible virtualized mod lists");
            Require(lists.All(e => e.Descendants().Any(c => c.Name.LocalName == nameof(VirtualizingStackPanel)) ||
                (!e.Descendants().Any(c => c.Name.LocalName.EndsWith(".ItemsPanel")) && list.ItemsPanel.Build() is VirtualizingStackPanel)),
                window + " keeps virtualization enabled");
        }
    }
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
