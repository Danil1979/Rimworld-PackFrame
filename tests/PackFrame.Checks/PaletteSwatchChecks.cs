using System.Reflection;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using PackFrame;

internal static class PaletteSwatchChecks
{
    internal static void Run()
    {
        static string Read(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UiWiring." + name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        var markup = XDocument.Parse(Read("ColorPaletteWindow.axaml"));
        XNamespace ui = "https://github.com/avaloniaui";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var theme = markup.Descendants(ui + "ControlTheme").Single(e => (string?)e.Attribute(x + "Key") == "PaletteSwatchButton");
        var surface = theme.Descendants(ui + "Border").Single(e => (string?)e.Attribute("Name") == "SwatchSurface");
        Require((string?)surface.Attribute("Background") == "{TemplateBinding Background}",
            "palette template displays the actual button color");
        foreach (var state in new[] { (":pointerover", "HoverOutline"), (":pressed", "PressedOutline"), (":focus-visible", "FocusOutline") })
        {
            var style = theme.Elements(ui + "Style").Single(e => ((string?)e.Attribute("Selector"))!.Contains(state.Item1));
            Require(((string?)style.Attribute("Selector"))!.EndsWith("Border#" + state.Item2) &&
                style.Elements(ui + "Setter").All(e => (string?)e.Attribute("Property") == "IsVisible"),
                "palette " + state.Item1 + " changes only its feedback outline, never the swatch fill");
        }
        var focus = theme.Descendants(ui + "Border").Single(e => (string?)e.Attribute("Name") == "FocusOutline");
        Require((string?)focus.Attribute("BorderBrush") == "White" &&
            (string?)focus.Element(ui + "Border")?.Attribute("BorderBrush") == "#FF101820",
            "palette keyboard focus has contrasting light and dark edges");
        var source = Read("ColorPaletteWindow.axaml.cs");
        Require(source.Contains("Theme = (ControlTheme)this.FindResource(\"PaletteSwatchButton\")!") &&
            source.Contains("Content = isCurrent ? CreateCurrentColorMarker() : null"),
            "palette swatches opt into dedicated feedback and mark only the current color");
        var marker = (Border)typeof(ColorPaletteWindow).GetMethod("CreateCurrentColorMarker", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        Require(marker.Child is Avalonia.Controls.Shapes.Path { Data: not null, StrokeThickness: 2 } &&
            marker.Background is ISolidColorBrush { Color: var color } && color == Colors.White &&
            marker.BorderBrush is ISolidColorBrush { Color: var edge } && edge == Color.Parse("#101820") &&
            !marker.IsHitTestVisible,
            "current color marker uses a real check shape with fixed contrast and does not intercept input");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
