using PackFrame;
using System.Xml.Linq;

internal static class ModPreviewPresentationChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        {
            Require(!string.IsNullOrWhiteSpace(ModPreviewPresentation.Status(language, ModPreviewState.Loading)) &&
                ModPreviewPresentation.Status(language, ModPreviewState.Loading) != ModPreviewPresentation.Status(language, ModPreviewState.Unavailable),
                "preview loading and failure have distinct localized status: " + language);
            Require(ModPreviewPresentation.Status(language, ModPreviewState.Loaded) == "" && ModPreviewPresentation.Status(language, ModPreviewState.None) == "",
                "loaded and absent previews have no stale status: " + language);
            Require(ModPreviewPresentation.ImageName(language, "Mod A").Contains("Mod A"), "preview accessibility identifies its mod: " + language);
        }
        Require(ModPreviewPresentation.CanReuse("same", "same", ModPreviewState.Loading), "same pending preview does not spawn duplicate decode work");
        Require(ModPreviewPresentation.CanReuse("same", "same", ModPreviewState.Loaded), "same loaded preview remains reusable");
        Require(!ModPreviewPresentation.CanReuse(null, null, ModPreviewState.Unavailable), "failed preview cannot leak its unavailable state into a no-preview mod");
        Require(!ModPreviewPresentation.CanReuse(null, "retry", ModPreviewState.Unavailable), "failed preview can be retried");
        Require(!ModPreviewPresentation.CanReuse("old", "new", ModPreviewState.Loaded), "changed preview requires a fresh request");
        using var markup = typeof(ModPreviewPresentationChecks).Assembly.GetManifestResourceStream("UiWiring.MainWindow.axaml")!;
        var document = XDocument.Load(markup); XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var frame = document.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "PreviewFrame");
        Require((string?)frame.Attribute("Height") == "170" && frame.Descendants().Any(e => (string?)e.Attribute(x + "Name") == "PreviewImage") &&
            frame.Descendants().Any(e => (string?)e.Attribute(x + "Name") == "PreviewStatus"), "preview image and status share a fixed-height slot to prevent text jumps");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}
