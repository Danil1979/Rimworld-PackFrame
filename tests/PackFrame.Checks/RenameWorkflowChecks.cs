using PackFrame;

internal static class RenameWorkflowChecks
{
    internal static void Run()
    {
        var instance = new ManagedInstance { FolderName = "profile1", DirectoryPath = "unused", Metadata = new InstanceMetadata { Name = "Colony", SteamCmd = true } };
        Check(instance.DisplayName != "Colony" && RenameWorkflow.EditableName(instance) == "Colony",
            "rename input excludes the generated SteamCMD display marker");
        instance.Metadata.Name = "My colony (cmd)";
        Check(RenameWorkflow.EditableName(instance) == "My colony (cmd)",
            "rename input preserves a marker deliberately included in the actual name");
        instance.Metadata.Name = "  ";
        Check(RenameWorkflow.EditableName(instance) == "profile1", "rename input falls back to the undecorated folder name");
        instance.Metadata = null;
        Check(RenameWorkflow.EditableName(instance) == "profile1", "legacy unnamed instances have a usable rename input");
        foreach (var language in new[] { "en", "zh-Hans", "zh-Hant", "ja" })
            Check(new[] { RenameWorkflow.Scope(language), RenameWorkflow.Folder(language), RenameWorkflow.Saving(language),
                RenameWorkflow.Failure(language) }.All(x => !string.IsNullOrWhiteSpace(x)),
                $"rename scope, destination, saving and recovery guidance are localized ({language})");
        Check(RenameWorkflow.Scope("en").Contains("display name") && RenameWorkflow.Failure("en").Contains("retry") &&
            RenameWorkflow.Failure("en").Contains("log"), "rename describes its scope and a concrete recovery route");
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }
}
