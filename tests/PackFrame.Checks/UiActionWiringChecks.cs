using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class UiActionWiringChecks
{
    internal static void Run()
    {
        static string Read(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("Missing UI wiring fixture: " + name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        var xaml = Read("UiWiring.MainWindow.axaml");
        var source = Read("UiWiring.MainWindow.axaml.cs");
        foreach (var (control, handler) in new[] {
            ("UndoButton", "OnUndoClick"), ("RedoButton", "OnRedoClick"),
            ("LanguageButton", "OnLanguageClick"), ("BrowseButton", "OnBrowseClick") })
            Require(Count(xaml, source, control, handler) == 1,
                $"{control} has exactly one click subscription across XAML and constructor wiring");

        const string markup = "<Window xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Button x:Name='Action' Click='Handle'/><Button x:Name='Other' Click='Handle'/></Window>";
        const string duplicate = "_action = Find<Button>(\"Action\"); _action.Click += Handle;";
        Require(Count(markup, duplicate, "Action", "Handle") == 2,
            "UI wiring regression detects the original XAML plus code double subscription");
        Require(Count(markup, duplicate, "Other", "Handle") == 1,
            "UI wiring regression distinguishes separate controls with the same handler");
        Require(Count(markup.Replace(" Click='Handle'", ""), duplicate, "Action", "Handle") == 1,
            "UI wiring regression accepts code-only subscriptions");
    }

    private static int Count(string xaml, string source, string control, string handler)
    {
        XNamespace names = "http://schemas.microsoft.com/winfx/2006/xaml";
        var count = XDocument.Parse(xaml).Descendants().Count(element =>
            (string?)element.Attribute(names + "Name") == control &&
            (string?)element.Attribute("Click") == handler);
        var fieldLookup = @"(?<field>\w+)\s*=\s*Find<Button>\(\s*""" + Regex.Escape(control) + @"""\s*\)";
        foreach (Match field in Regex.Matches(source, fieldLookup))
            count += Regex.Matches(source, @"\b" + Regex.Escape(field.Groups["field"].Value) +
                @"\.Click\s*\+=\s*" + Regex.Escape(handler) + @"\s*;").Count;
        return count;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
