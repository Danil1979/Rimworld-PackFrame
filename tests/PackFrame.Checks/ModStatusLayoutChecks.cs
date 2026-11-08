using System.Reflection;
using System.Xml.Linq;
using PackFrame;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

internal static class ModStatusLayoutChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            var label = new string('名', 80);
            var author = new string('著', 80);
            var package = "example." + new string('p', 160);
            var link = "https://example.com/mod?name=" + new string('x', 180);
            var original = new MissingModRow { Label = label, Author = author, PackageId = package,
                Link = link, Availability = WorkshopModAvailability.Missing };
            var display = ModStatusDisplayRow.From(original, language);
            Require(display.Label == label && display.Author == author && display.PackageId == package && display.Link == link,
                "grouped identity preserves complete names, author, package ID and raw URL in " + language);
            Require(display.AuthorLabel == Localization.FieldAuthor(language) + ":" &&
                display.PackageLabel == Localization.FieldPackageId(language) + ":",
                "secondary identity fields have localized labels in " + language);
            Require(ModStatusDisplayRow.SearchPlaceholder(language, true) != ModStatusDisplayRow.SearchPlaceholder(language, false),
                "history search distinguishes dates from availability status in " + language);
            Require(!string.IsNullOrWhiteSpace(ModStatusDisplayRow.EmptyAvailabilityText(language)),
                "no-entry availability feedback is available in " + language);
        }
        Require(ModStatusDisplayRow.SearchPlaceholder("en", true).Contains("date") &&
            !ModStatusDisplayRow.SearchPlaceholder("en", true).Contains("status"), "history search describes the actual date field");
        Require(ModStatusDisplayRow.EmptyAvailabilityText("en") == "No mod availability entries to display.",
            "empty availability feedback does not claim that installation has succeeded");

        // Real Avalonia layout, without opening a window: the scroll viewport is
        // wider than its padded content in both short and overflowing lists.
        foreach (var width in new[] { 1006d, 746d })
        foreach (var count in new[] { 1, 12 })
        {
            var panel = new StackPanel();
            for (var i = 0; i < count; i++) panel.Children.Add(new Border { Height = 99 });
            var presenter = new ScrollContentPresenter { Content = panel, Padding = new Thickness(0, 0, 16, 0) };
            presenter.UpdateChild();
            presenter.Measure(new Size(width, 200));
            presenter.Arrange(new Rect(0, 0, width, 200));
            var header = new Border { Padding = new Thickness(12, 9) };
            ModStatusList.AlignHeaderToRows(header, panel);
            Require(panel.Bounds.Width == width - 16 && header.Width == panel.Children[0].Bounds.Width,
                $"header matches actual padded row width at {width}px with {count} rows (panel={panel.Bounds.Width}, row={panel.Children[0].Bounds.Width}, header={header.Width})");
            presenter.Measure(new Size(width - 40, 200));
            presenter.Arrange(new Rect(0, 0, width - 40, 200));
            ModStatusList.AlignHeaderToRows(header, panel);
            Require(header.Width == panel.Children[0].Bounds.Width && header.Width == width - 56,
                $"header follows resized padded rows at {width}px with {count} rows");
        }

        XNamespace names = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var window in new[] { "MissingModsWindow", "MissingHistoryWindow" })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UiWiring." + window + ".axaml")!;
            var document = XDocument.Load(stream);
            var list = document.Descendants().Single(element => (string?)element.Attribute(names + "Name") == "Rows");
            Require(list.Descendants().Any(element => element.Name.LocalName == "VirtualizingStackPanel") &&
                (string?)list.Attribute("AutoScrollToSelectedItem") == "False", window + " retains virtualization and prevents selection-driven scroll jumps");
            var header = document.Descendants().Single(element => (string?)element.Attribute(names + "Name") == "LabelHeader");
            Require((string?)header.Parent?.Parent?.Attribute(names + "Name") == "RowHeaderContent" &&
                (string?)header.Parent?.Parent?.Attribute("Padding") == "12,9",
                window + " exposes the padded header for alignment with actual laid-out rows");
            var template = list.Descendants().Single(element => element.Name.LocalName == "DataTemplate");
            foreach (var field in new[] { "Label", "Author", "PackageId", "Link" })
                Require(template.Descendants().Any(element => element.Name.LocalName == "SelectableTextBlock" &&
                    (string?)element.Attribute("Text") == "{Binding Snapshot." + field + "}" &&
                    (string?)element.Attribute("TextWrapping") == "Wrap"), window + " keeps exact " + field + " selectable and wrapping");
            var url = template.Descendants().Single(element => (string?)element.Attribute("Text") == "{Binding Snapshot.Link}");
            Require((string?)url.Attribute("Grid.ColumnSpan") == "3" && (string?)url.Attribute("Grid.Row") == "1",
                window + " gives raw URLs the full row width below the primary scan line");
            var action = template.Descendants().Single(element => element.Name.LocalName == "Button");
            Require((string?)action.Attribute("Click") == "OnOpenLinkClick" &&
                (string?)action.Attribute("IsEnabled") == "{Binding Snapshot.CanOpenLink}" &&
                (string?)action.Attribute("AutomationProperties.Name") == "{Binding Snapshot.LinkAccessibleName}",
                window + " retains one accessible safe web action for selected-row keyboard navigation");
            Require((string?)action.Attribute("Padding") == "10,6" && (string?)action.Attribute("MinHeight") == "32",
                window + " reserves room for localized link labels while retaining a usable target height");
            var count = document.Descendants().Single(element => (string?)element.Attribute(names + "Name") == "FilterCountText");
            Require(count.Parent?.Name.LocalName == "WrapPanel" && count.Parent.Elements().Any(element =>
                (string?)element.Attribute(names + "Name") == "SearchHintText"), window + " shares search feedback space without removing wrapping guidance");
        }
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new Exception("Failed: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
