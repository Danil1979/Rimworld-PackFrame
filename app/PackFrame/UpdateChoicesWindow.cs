using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace PackFrame;

internal sealed record UpdateChoices(HashSet<string> KeepLocal, bool KeepOrder)
{
    internal static UpdateChoices FromReplacements(
        IEnumerable<KeyValuePair<string, bool>> replacements, bool keepOrder)
    {
        var keep = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (path, replace) in replacements)
            if (!replace) keep.Add(path);
        return new UpdateChoices(keep, keepOrder);
    }
}

internal sealed class UpdateChoicesWindow : Window
{
    public UpdateChoicesWindow(PackUpdatePlan plan, string? language)
    {
        string T(string en, string zh, string hant, string ja) => OperationText.Choose(language, en, zh, hant, ja);
        Title = T("Review pack update", "检查合集更新", "檢查合集更新", "パック更新を確認");
        Width = 650; Height = 480;
        MinWidth = 480; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = ThemeService.BackgroundBrush;
        Foreground = ThemeService.PrimaryTextBrush;
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = T($"Added mods: {plan.Added.Count} · Removed mods: {plan.Removed.Count}",
                $"新增模组：{plan.Added.Count} · 移除模组：{plan.Removed.Count}",
                $"新增模組：{plan.Added.Count} · 移除模組：{plan.Removed.Count}",
                $"追加 MOD: {plan.Added.Count} · 削除 MOD: {plan.Removed.Count}"),
            FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap
        });
        if (plan.Added.Count + plan.Removed.Count > 0)
            panel.Children.Add(new SelectableTextBlock
            {
                Text = string.Join("\n", plan.Added.ConvertAll(id => "+ " + id)) +
                    (plan.Added.Count > 0 && plan.Removed.Count > 0 ? "\n" : string.Empty) +
                    string.Join("\n", plan.Removed.ConvertAll(id => "- " + id)),
                TextWrapping = TextWrapping.Wrap
            });
        if (plan.Conflicts.Count > 0)
            panel.Children.Add(new TextBlock
            {
                Text = T("These files have local changes or already exist locally. Checked items will be replaced or removed by the update. Uncheck an item to keep your local copy.",
                    "以下文件已在本地修改或已存在。勾选项将被更新替换或移除；取消勾选可保留本地副本。",
                    "以下檔案已在本機修改或已存在。勾選項目將被更新取代或移除；取消勾選可保留本機副本。",
                    "以下のファイルはローカルで変更済み、または既に存在します。チェックした項目は更新で置換または削除されます。ローカル版を残すにはチェックを外してください。"),
                TextWrapping = TextWrapping.Wrap
            });
        var checks = new Dictionary<string, CheckBox>();
        foreach (var path in plan.Conflicts)
        {
            var label = T("Use update for: ", "使用更新版本：", "使用更新版本：", "更新版を使用: ") + path;
            var check = new CheckBox
            {
                Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
                IsChecked = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(check, label);
            checks[path] = check;
            panel.Children.Add(check);
        }
        var orderText = T(
            "Keep my order (new mods may not be enabled)", "保留当前排序（新增模组可能未启用）",
            "保留目前排序（新增模組可能未啟用）", "現在の並び順を保持（新 MOD は未有効の場合あり）");
        var order = new CheckBox
        {
            Content = new TextBlock { Text = orderText, TextWrapping = TextWrapping.Wrap },
            IsVisible = plan.OrderChanged,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(order, orderText);
        panel.Children.Add(order);
        panel.Children.Add(new TextBlock
        {
            Text = plan.OrderChanged
                ? T("If unchecked, the update's mod list and load order will replace yours.",
                    "若不勾选，更新中的模组列表和加载顺序将替换当前列表。",
                    "若不勾選，更新中的模組清單與載入順序將取代目前清單。",
                    "チェックを外すと、更新の MOD リストと読み込み順で現在のリストを置き換えます。")
                : T("The update's mod list and load order will be applied.",
                    "将应用更新中的模组列表和加载顺序。", "將套用更新中的模組清單與載入順序。",
                    "更新の MOD リストと読み込み順を適用します。"),
            TextWrapping = TextWrapping.Wrap
        });
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0)
        };
        var theme = Application.Current?.FindResource("FlatButton") as ControlTheme;
        var cancel = new Button { Content = Localization.CancelButton(language), IsCancel = true, Theme = theme, MinHeight = 32 };
        cancel.Click += (_, _) => Close();
        var apply = new Button { Content = T("Apply update", "应用更新", "套用更新", "更新を適用"), Theme = theme, MinHeight = 32 };
        apply.Click += (_, _) =>
        {
            var replacements = new List<KeyValuePair<string, bool>>();
            foreach (var (path, check) in checks) replacements.Add(new(path, check.IsChecked == true));
            Close(UpdateChoices.FromReplacements(replacements, order.IsChecked == true));
        };
        buttons.Children.Add(cancel); buttons.Children.Add(apply);
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(16) };
        layout.Children.Add(new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid.SetRow(buttons, 1);
        layout.Children.Add(buttons);
        Content = layout;
        Opened += (_, _) => cancel.Focus();
    }
}
