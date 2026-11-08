using Avalonia.Automation;
using Avalonia.Controls;

namespace PackFrame;

internal sealed record PaneAccessibility(string List, string Search, string SearchField, string Instance, string Manage)
{
    internal static PaneAccessibility Create(string? language, int column, string? instanceName)
    {
        var role = column switch
        {
            0 => OperationText.Choose(language, "Available mods", "可用模组", "可用模組", "利用可能な MOD"),
            1 => OperationText.Choose(language, "Load order", "加载顺序", "載入順序", "読み込み順序"),
            _ => OperationText.Choose(language, "Comparison load order", "对比加载顺序", "比較載入順序", "比較用の読み込み順序")
        };
        var context = column == 0 || string.IsNullOrWhiteSpace(instanceName) ? role : $"{role}: {instanceName}";
        return new(context,
            OperationText.Choose(language, "Search", "搜索", "搜尋", "検索") + ": " + context,
            OperationText.Choose(language, "Search field", "搜索字段", "搜尋欄位", "検索項目") + ": " + context,
            OperationText.Choose(language, "Choose instance", "选择实例", "選擇實例", "インスタンスを選択") + ": " + context,
            OperationText.Choose(language, "Manage instance", "管理实例", "管理實例", "インスタンスの管理") + ": " + context);
    }

    internal void Apply(ListBox list, TextBox search, ComboBox field, ComboBox? instance = null, Button? manage = null)
    {
        AutomationProperties.SetName(list, List);
        AutomationProperties.SetName(search, Search);
        AutomationProperties.SetName(field, SearchField);
        if (instance is not null) AutomationProperties.SetName(instance, Instance);
        if (manage is not null) AutomationProperties.SetName(manage, Manage);
    }
}
