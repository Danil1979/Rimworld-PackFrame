using System.IO;
using System.Linq;
using Avalonia.Platform.Storage;

namespace PackFrame;

internal static class ProfileExport
{
    internal static FilePickerSaveOptions Options(string displayName, string fallbackName, bool rml, string? language)
    {
        // Portable suggestions also remain usable when profiles move between systems.
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var name = new string(displayName.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (name.Length == 0) name = fallbackName;
        var extension = rml ? "rml" : "xml";
        var format = rml ? Localization.ExportRml(language) : Localization.ExportXml(language);
        return new FilePickerSaveOptions
        {
            Title = Localization.ExportInstance(language) + ": " + displayName,
            SuggestedFileName = name + "." + extension,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType(format) { Patterns = new[] { "*." + extension } } }
        };
    }

    internal static string PickerUnavailable(string? language) => OperationText.Choose(language,
        "The save dialog is unavailable. No profile was exported.",
        "保存对话框不可用。未导出配置。", "儲存對話框無法使用。未匯出設定。", "保存ダイアログを使用できません。設定はエクスポートされていません。");

    internal static string LocalFileRequired(string? language) => OperationText.Choose(language,
        "Choose a file on this computer to export the profile.",
        "请选择此电脑上的文件以导出配置。", "請選擇此電腦上的檔案以匯出設定。", "設定のエクスポート先には、このコンピューター上のファイルを選択してください。");
}
