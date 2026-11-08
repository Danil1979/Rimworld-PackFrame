namespace PackFrame;

internal sealed record AvailableTargetGuidance(string Subtitle, string Action, string Help)
{
    internal static AvailableTargetGuidance Create(string? language, string? name, string? configPath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            var choose = OperationText.Choose(language, "Choose a target instance", "选择目标实例", "選擇目標實例", "対象インスタンスを選択");
            return new(choose, choose, choose);
        }
        var subtitle = OperationText.Choose(language, "Not enabled in:", "尚未启用于：", "尚未啟用於：", "このインスタンスで無効：") + "\n" + name;
        var action = OperationText.Choose(language, "Enter / double-click →", "Enter / 双击 →", "Enter / 按兩下 →", "Enter / ダブルクリック →") + "\n" + name;
        var help = OperationText.Choose(language,
            $"This catalog shows mods not enabled in {name}. Enter or double-click enables the selection there. To choose another destination, use Enable above that instance's list.",
            $"此目录显示尚未在 {name} 中启用的模组。Enter 或双击会在该实例中启用所选模组。要使用其他目标，请点击该实例列表上方的“启用”。",
            $"此目錄顯示尚未在 {name} 中啟用的模組。Enter 或按兩下會在該實例中啟用所選模組。要使用其他目標，請點選該實例清單上方的「啟用」。",
            $"この一覧には {name} で無効な MOD を表示します。Enter またはダブルクリックで選択をこのインスタンスに有効化します。別の移動先は、そのリストの上の「有効化」で指定できます。");
        return new(subtitle, action, help + (string.IsNullOrWhiteSpace(configPath) ? string.Empty : "\n\n" + configPath));
    }
}
