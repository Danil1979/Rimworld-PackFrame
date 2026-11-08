using System;
using System.IO;

namespace PackFrame;

internal sealed record InstanceImportReview(string SourcePath, string TargetName, string DestinationPath,
    bool IsArchive, bool IsOfficial, bool HasPendingChanges)
{
    internal static InstanceImportReview Create(string source, ManagedInstance target, bool hasPendingChanges)
    {
        var archive = Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        return new(Path.GetFullPath(source), target.DisplayName,
            Path.GetFullPath(archive ? target.DirectoryPath : Path.Combine(target.ConfigDirectoryPath, "ModsConfig.xml")),
            archive, target.IsOfficial, hasPendingChanges);
    }

    internal static string SourceLabel(string? language) => OperationText.Choose(language,
        "Import from", "导入来源", "匯入來源", "インポート元");
    internal static string TargetLabel(string? language) => OperationText.Choose(language,
        "Replace in", "替换目标", "取代目標", "置き換え先");
    internal static string ScopeLabel(string? language) => OperationText.Choose(language,
        "What will change", "将修改的内容", "將修改的內容", "変更される内容");
    internal static string NewLabel(string? language) => OperationText.Choose(language,
        "Create new instance", "新建独立实例", "建立獨立實例", "新規インスタンスを作成");
    internal static string ReviewLabel(string? language) => OperationText.Choose(language,
        "Review overwrite…", "查看替换详情…", "檢視取代詳情…", "置き換えを確認…");
    internal static string Hint(string? language) => OperationText.Choose(language,
        "Create a separate instance, or review changes to the selected instance before replacing it.",
        "新建独立实例，或查看所选实例的替换内容后再确认。",
        "建立獨立實例，或檢視所選實例的取代內容後再確認。",
        "別のインスタンスを作成するか、選択中のインスタンスの変更内容を確認して置き換えます。");
    internal string Scope(string? language) => !IsArchive
        ? OperationText.Choose(language,
            "Replaces the mod list and load order. Other configuration files, local mods and saves are kept.",
            "替换模组列表及加载顺序。保留其他配置文件、本地模组和存档。",
            "取代模組清單及載入順序。保留其他設定檔案、本機模組和存檔。",
            "MOD リストとロード順を置き換えます。他の設定ファイル、ローカル MOD、セーブは保持されます。")
        : IsOfficial
            ? OperationText.Choose(language,
                "Applies the archive's Config files to this official configuration folder. Matching files are overwritten; unrelated files and saves are kept. Archive Mods and Saves are not imported.",
                "将压缩包中的 Config 文件应用到此官方配置文件夹。同名文件会被覆盖；保留其他文件和存档。不导入压缩包中的 Mods 和 Saves。",
                "將壓縮檔中的 Config 檔案套用到此官方設定資料夾。同名檔案會被覆寫；保留其他檔案和存檔。不匯入壓縮檔中的 Mods 和 Saves。",
                "アーカイブの Config をこの公式設定フォルダーに適用します。同名ファイルは上書きされます。他のファイルとセーブは保持され、アーカイブ内の Mods と Saves はインポートされません。")
            : OperationText.Choose(language,
                "Replaces the entire instance folder with the archive, including Config, local mods and saves. Existing files absent from the archive will be removed.",
                "用压缩包替换整个实例文件夹，包括 Config、本地模组和存档。压缩包中没有的现有文件将被删除。",
                "以壓縮檔取代整個實例資料夾，包括 Config、本機模組和存檔。壓縮檔中沒有的現有檔案將被移除。",
                "Config、ローカル MOD、セーブを含むインスタンスフォルダー全体をアーカイブで置き換えます。アーカイブにない既存ファイルは削除されます。");
    internal string PendingWarning(string? language) => HasPendingChanges ? OperationText.Choose(language,
        "Unsaved mod-list changes for this instance will also be replaced.",
        "此实例尚未保存的模组列表修改也会被替换。",
        "此實例尚未儲存的模組清單修改也會被取代。",
        "このインスタンスの未保存の MOD リスト変更も置き換えられます。") : string.Empty;
    internal string Confirmation(string? language) => string.Join("\n\n", new[] {
        SourceLabel(language) + "\n" + SourcePath,
        TargetLabel(language) + "\n" + TargetName + "\n" + DestinationPath,
        Scope(language), PendingWarning(language) }).TrimEnd();
}
