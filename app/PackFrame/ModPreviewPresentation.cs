namespace PackFrame;

internal enum ModPreviewState { None, Loading, Loaded, Unavailable }

internal static class ModPreviewPresentation
{
    internal static bool CanReuse(string? currentPath, string? nextPath, ModPreviewState state) =>
        currentPath == nextPath && state != ModPreviewState.Unavailable;

    internal static string Status(string? language, ModPreviewState state) => state switch
    {
        ModPreviewState.Loading => OperationText.Choose(language, "Loading preview…", "正在加载预览…", "正在載入預覽…", "プレビューを読み込み中…"),
        ModPreviewState.Unavailable => OperationText.Choose(language, "Preview unavailable", "预览不可用", "無法顯示預覽", "プレビューを表示できません"),
        _ => string.Empty
    };

    internal static string ImageName(string? language, string? name) =>
        OperationText.Choose(language, "Mod preview", "模组预览", "模組預覽", "MOD プレビュー") + ": " + name;
}
