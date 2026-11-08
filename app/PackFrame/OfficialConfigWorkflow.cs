using System.Collections.Generic;
using System.Linq;

namespace PackFrame;

internal enum OfficialConfigChange { Unchanged, Applied, PendingChanges, SaveFailed }

internal static class OfficialConfigWorkflow
{
    internal static OfficialConfigChange TryChange(AppConfig config, string directory,
        IEnumerable<string> pendingDirectories, string? settingsPath = null)
    {
        if (config.IsCurrentOfficialConfigPath(directory))
            return config.TrySetOfficialConfigPath(directory, settingsPath)
                ? OfficialConfigChange.Unchanged : OfficialConfigChange.SaveFailed;
        // A draft belongs to its original destination. Do not hide it by replacing
        // the sole official instance while that destination still has unsaved edits.
        if (pendingDirectories.Any(config.IsCurrentOfficialConfigPath))
            return OfficialConfigChange.PendingChanges;
        return config.TrySetOfficialConfigPath(directory, settingsPath)
            ? OfficialConfigChange.Applied : OfficialConfigChange.SaveFailed;
    }

    internal static string UseFolder(string? language) => OperationText.Choose(language,
        "Use this folder", "使用此文件夹", "使用此資料夾", "このフォルダーを使用");
    internal static string CurrentFolder(string? language) => OperationText.Choose(language,
        "Current folder", "当前文件夹", "目前資料夾", "現在のフォルダー");
    internal static string NewFolder(string? language) => OperationText.Choose(language,
        "Configuration folder", "配置文件夹", "設定資料夾", "設定フォルダー");
    internal static string SaveFailure(string? language) => OperationText.Choose(language,
        "Could not save the folder setting. Your current folder has not changed. Check write access to the PackFrame folder, then try again.",
        "无法保存文件夹设置，当前文件夹未更改。请检查 PackFrame 文件夹的写入权限后重试。",
        "無法儲存資料夾設定，目前資料夾未變更。請檢查 PackFrame 資料夾的寫入權限後重試。",
        "フォルダー設定を保存できませんでした。現在のフォルダーは変更されていません。PackFrame フォルダーの書き込み権限を確認して再試行してください。");
    internal static string PendingWarning(string? language, string? currentPath) => OperationText.Choose(language,
        "The current folder has unsaved mod-list changes. Cancel this dialog and save or undo those edits before switching folders. Your edits are kept in:\n" + currentPath,
        "当前文件夹有未保存的模组列表修改。请取消此对话框，保存或撤销修改后再切换文件夹。修改已保留在：\n" + currentPath,
        "目前資料夾有未儲存的模組清單修改。請取消此對話方塊，儲存或復原修改後再切換資料夾。修改已保留在：\n" + currentPath,
        "現在のフォルダーに未保存の MOD リスト変更があります。この画面をキャンセルし、変更を保存または元に戻してから切り替えてください。編集の保存先：\n" + currentPath);
}
