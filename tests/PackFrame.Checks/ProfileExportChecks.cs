using PackFrame;

internal static class ProfileExportChecks
{
    internal static void Run()
    {
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
        foreach (var rml in new[] { false, true })
        {
            var options = ProfileExport.Options("Colony A", "profile1", rml, language);
            var extension = rml ? "rml" : "xml";
            Require(options.ShowOverwritePrompt == true && options.Title!.Contains("Colony A"), "export identifies target and requests replacement consent: " + language + extension);
            Require(options.SuggestedFileName == "Colony A." + extension && options.DefaultExtension == extension &&
                options.FileTypeChoices!.Single().Patterns!.Single() == "*." + extension, "export format and filename agree: " + language + extension);
            Require(!string.IsNullOrWhiteSpace(ProfileExport.PickerUnavailable(language)) && !string.IsNullOrWhiteSpace(ProfileExport.LocalFileRequired(language)), "export errors have actionable localized feedback: " + language + extension);
        }
        Require(ProfileExport.Options("a/b\\c:d*e?f\"g<h>i|j", "profile1", false, "en").SuggestedFileName == "a_b_c_d_e_f_g_h_i_j.xml", "export suggestions sanitize portable filename characters");
        Require(ProfileExport.Options(" ... ", "profile1", true, "en").SuggestedFileName == "profile1.rml", "export empty filename falls back to instance identifier");
        var root = Path.Combine(Path.GetTempPath(), "packframe-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "existing.xml");
            File.WriteAllText(path, "previous snapshot");
            try { FileTransactions.WriteAtomic(path, temporary => { File.WriteAllText(temporary, "partial"); throw new IOException("Simulated serialization failure"); }); }
            catch (IOException) { }
            Require(File.ReadAllText(path) == "previous snapshot" && Directory.GetFiles(root).Length == 1, "failed profile write preserves original and cleans staging");
            FileTransactions.WriteAtomic(path, temporary => File.WriteAllText(temporary, "complete snapshot"));
            Require(File.ReadAllText(path) == "complete snapshot" && Directory.GetFiles(root).Length == 1, "successful profile replacement publishes complete snapshot");
        }
        finally { Directory.Delete(root, true); }
        using var stream = typeof(ProfileExportChecks).Assembly.GetManifestResourceStream("UiWiring.MainWindow.axaml.cs")!;
        var source = new StreamReader(stream).ReadToEnd();
        var export = source[source.IndexOf("private async Task ExportProfileAsync")..source.IndexOf("private static ulong ExportSteamId")];
        Require(export.Contains("SaveFilePickerAsync(options)") && export.Contains("selectedFile is null || _windowClosed") && export.Contains("FileTransactions.WriteAtomic(path"), "export cancels without writing and uses atomic publication");
        Require(export.Contains("_exportProfileInProgress") && export.Contains("finally { _exportProfileInProgress = false; }"), "export prevents duplicate dialogs and always releases guard");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}
