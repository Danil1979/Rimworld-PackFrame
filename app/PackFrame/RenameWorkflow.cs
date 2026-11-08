namespace PackFrame;

internal static class RenameWorkflow
{
    internal static string EditableName(ManagedInstance instance) =>
        string.IsNullOrWhiteSpace(instance.Metadata?.Name) ? instance.FolderName : instance.Metadata.Name.Trim();

    internal static string Scope(string? language) => OperationText.Choose(language,
        "Change the display name. The instance folder and mod list stay the same.",
        "更改显示名称。实例文件夹和模组列表保持不变。",
        "變更顯示名稱。執行個體資料夾和模組清單保持不變。",
        "表示名を変更します。インスタンスのフォルダーと MOD リストは変更しません。");

    internal static string Folder(string? language) => OperationText.Choose(language,
        "Instance folder", "实例文件夹", "執行個體資料夾", "インスタンスのフォルダー");

    internal static string Saving(string? language) => OperationText.Choose(language,
        "Saving name…", "正在保存名称…", "正在儲存名稱…", "名前を保存中…");

    internal static string Failure(string? language) => OperationText.Choose(language,
        "The name could not be saved. Your text is still here. Check the instance folder's permissions and metadata, then retry. Open the log for details.",
        "名称未能保存。已保留输入内容，请检查实例文件夹权限和元数据后重试。打开日志可查看详情。",
        "名稱未能儲存。已保留輸入內容，請檢查執行個體資料夾權限和中繼資料後重試。開啟記錄可查看詳情。",
        "名前を保存できませんでした。入力内容は保持されています。インスタンスのフォルダー権限とメタデータを確認して再試行してください。詳細はログで確認できます。");
}
