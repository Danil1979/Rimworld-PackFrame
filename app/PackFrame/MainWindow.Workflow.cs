using System;
using System.IO;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PackFrame;

public partial class MainWindow
{
    private long _lastSelectionAction;

    private string WorkflowText(string en, string hans, string hant, string ja) =>
        OperationText.Choose(_config.Language, en, hans, hant, ja);

    private (ListBox List, TextBox Search, ModSearchState State, ProblemFilterState Filter, Button Source) PaneWorkflow(int column) => column switch
    {
        1 => (_activeList1, _activeSearchBox1, _activeSearch1, _activeFilter1, _activeSourceButton1),
        2 => (_activeList2, _activeSearchBox2, _activeSearch2, _activeFilter2, _activeSourceButton2),
        _ => (_inactiveList, _inactiveSearchBox, _inactiveSearch, _inactiveFilter, _inactiveSourceButton)
    };

    private static bool HasPaneFilters(ModSearchState search, ProblemFilterState filter) =>
        !string.IsNullOrEmpty(search.Query) || search.SourceIndex != 0 ||
        filter.Sort != ProblemFilterMode.Off || filter.Version != ProblemFilterMode.Off ||
        filter.Duplicate != ProblemFilterMode.Off || filter.Local != ProblemFilterMode.Off;

    private string PaneCount(int column, int visible)
    {
        var pane = PaneWorkflow(column);
        return HasPaneFilters(pane.State, pane.Filter) && _searchSources.TryGetValue(pane.List, out var source)
            ? $"{visible} / {source.Count}" : visible.ToString();
    }

    private void UpdatePaneWorkflow()
    {
        for (var column = 0; column < 3; column++)
        {
            var pane = PaneWorkflow(column);
            var filtered = HasPaneFilters(pane.State, pane.Filter);
            var reset = Find<Button>($"ClearPaneFilters{column}");
            reset.IsEnabled = filtered;
            var resetHint = WorkflowText("Clear this pane's search, source and status filters (Esc).",
                "清除此栏的搜索、来源及状态筛选（Esc）。", "清除此欄的搜尋、來源及狀態篩選（Esc）。", "このペインの検索、ソース、状態フィルターを解除（Esc）。");
            ToolTip.SetTip(reset, resetHint);
            AutomationProperties.SetName(reset, resetHint);
            var empty = Find<Border>($"PaneEmpty{column}");
            empty.IsVisible = pane.List.ItemCount == 0 && !_loadingMods;
            Find<TextBlock>($"PaneEmptyTitle{column}").Text = filtered
                ? WorkflowText("No matching mods", "没有匹配的模组", "沒有符合的模組", "一致する MOD がありません")
                : WorkflowText("No mods here yet", "此处暂无模组", "此處尚無模組", "まだ MOD がありません");
            Find<TextBlock>($"PaneEmptyBody{column}").Text = _gameInstallation is null
                ? WorkflowText("Choose a RimWorld folder above to load your library.", "在上方选择 RimWorld 文件夹以加载模组库。", "在上方選擇 RimWorld 資料夾以載入模組庫。", "上部で RimWorld フォルダーを選択してライブラリを読み込みます。")
                : filtered
                    ? WorkflowText("Press Esc in this search field, or use its × to clear search and filters.", "在搜索框中按 Esc，或点击末尾的 × 清除搜索和筛选。", "在搜尋框中按 Esc，或點選末尾的 × 清除搜尋和篩選。", "検索欄で Esc を押すか、末尾の × で検索とフィルターを解除します。")
                    : column == 0
                        ? WorkflowText("Every available mod is already active in the selected instance.", "所选实例已启用所有可用模组。", "所選實例已啟用所有可用模組。", "選択中のインスタンスでは、利用できる MOD がすべて有効です。")
                        : WorkflowText("Select mods in Inactive, then choose Enable above this list.", "在未启用列表中选择模组，然后点击此列表上方的“启用”按钮。", "在未啟用清單中選擇模組，然後點選此清單上方的「啟用」按鈕。", "無効リストで MOD を選び、このリストの上の「有効化」で追加します。");

        }
        UpdateSelectionAction();
    }

    private void OnResetPaneFiltersClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var column)) return;
        ClearPaneFilters(column);
        e.Handled = true;
    }

    private void ClearPaneFilters(int column)
    {
        var pane = PaneWorkflow(column);
        if (_searchTimers.TryGetValue(pane.List, out var timer)) timer.Stop();
        SetPaneInteractionContext(pane.List);
        pane.State.Query = string.Empty;
        pane.State.SourceIndex = 0;
        pane.Filter.Sort = pane.Filter.Version = pane.Filter.Duplicate = pane.Filter.Local = ProblemFilterMode.Off;
        pane.Search.Text = string.Empty;
        if (_searchTimers.TryGetValue(pane.List, out timer)) timer.Stop();
        UpdateSourceButton(pane.Source, pane.State, _config.Language);
        if (_allMods.Count > 0) RefreshSearch(pane.List);
        else UpdatePaneWorkflow();
        pane.Search.Focus();
    }

    private void UpdateSelectionAction()
    {
        for (var column = 1; column <= 2; column++)
        {
            var instance = GetInstanceForColumn(column);
            var target = instance?.DisplayName ?? WorkflowText("Choose an instance", "选择实例", "選擇實例", "インスタンスを選択");
            Find<TextBlock>($"PaneActionTarget{column}").Text = target;
            foreach (var enabling in new[] { true, false })
            {
                var list = enabling ? _inactiveList : PaneWorkflow(column).List;
                var count = list.SelectedItems?.Count ?? 0;
                var action = Find<Button>($"{(enabling ? "Enable" : "Disable")}Selection{column}");
                action.Content = enabling
                    ? WorkflowText($"Enable {count} →", $"启用 {count} →", $"啟用 {count} →", $"有効化 {count} →")
                    : WorkflowText($"Disable {count}", $"禁用 {count}", $"停用 {count}", $"無効化 {count}");
                action.IsEnabled = count > 0 && instance is not null && !instance.IsDamaged &&
                    !_loadingMods && !_painting && !_customDragActive && !_instanceWriteInProgress;
                var hint = $"{action.Content}: {target}";
                ToolTip.SetTip(action, hint);
                AutomationProperties.SetName(action, hint);
            }
        }
    }

    internal static bool TryGetPaneActionRoute(string? tag, out int source, out int target)
    {
        source = target = 0;
        var parts = tag?.Split(':');
        return parts?.Length == 2 && int.TryParse(parts[0], out source) && int.TryParse(parts[1], out target) &&
            ((source == 0 && target is 1 or 2) || (source is 1 or 2 && target == 0));
    }

    private void OnSelectionActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { IsEnabled: true } button || _customDragActive || _painting ||
            _loadingMods || _instanceWriteInProgress) return;
        if (!TryGetPaneActionRoute(button.Tag?.ToString(), out var source, out var target)) return;
        var list = PaneWorkflow(source).List;
        if (!list.IsEffectivelyVisible || (target == 2 && !_config.ShowSecondaryPane)) return;
        var now = Environment.TickCount64;
        if (now - _lastSelectionAction < 400) return;
        _lastSelectionAction = now;
        var mods = GetSelectedMods(list);
        if (mods.Count == 0) return;
        var targetList = target > 0 ? GetModListForColumn(target) : null;
        MoveMods(new DragPayload(source, mods), target,
            targetList is null ? 0 : MapDropIndexToActive(targetList, targetList.ItemCount));
        UpdateSelectionAction();
        e.Handled = true;
    }

    private void UpdateEditStatus()
    {
        var pending = InstanceStore.PendingConfigDirectories;
        var names = pending.Select(path => _instances.FirstOrDefault(instance =>
            !string.IsNullOrWhiteSpace(instance.ConfigDirectoryPath) &&
            string.Equals(Path.GetFullPath(instance.ConfigDirectoryPath), path, StringComparison.OrdinalIgnoreCase))?.DisplayName
            ?? Path.GetFileName(Path.GetDirectoryName(path)) ?? path).ToList();
        var text = pending.Count > 0
            ? WorkflowText($"Unsaved mod lists: {string.Join(", ", names)} · Save applies to all edited instances",
                $"未保存的模组列表：{string.Join("、", names)} · 保存将应用于所有已编辑实例",
                $"未儲存的模組清單：{string.Join("、", names)} · 儲存將套用至所有已編輯實例",
                $"未保存の MOD リスト: {string.Join(", ", names)} · 保存は編集した全インスタンスに適用")
            : WorkflowText("No unsaved mod-list changes", "模组列表没有未保存的修改", "模組清單沒有未儲存的修改", "MOD リストに未保存の変更はありません");
        Find<Border>("EditStatusBanner").IsVisible = pending.Count > 0;
        for (var column = 1; column <= 2; column++)
        {
            var instance = GetInstanceForColumn(column);
            var dirty = instance is not null && !string.IsNullOrWhiteSpace(instance.ConfigDirectoryPath) &&
                pending.Contains(Path.GetFullPath(instance.ConfigDirectoryPath), StringComparer.OrdinalIgnoreCase);
            Find<Border>($"PendingBadge{column}").IsVisible = dirty;
            Find<TextBlock>($"PendingLabel{column}").Text = WorkflowText("Unsaved", "未保存", "未儲存", "未保存");
        }
        _saveButton.Classes.Set("primary-action", pending.Count > 0);
        var status = Find<TextBlock>("EditStatusText");
        status.Text = text;
        status.FontWeight = pending.Count > 0 ? FontWeight.SemiBold : FontWeight.Normal;
        ToolTip.SetTip(status, text);
        ToolTip.SetTip(_saveButton, WorkflowText("Save all edited mod lists (Ctrl+S / ⌘S)",
            "保存所有已编辑的模组列表（Ctrl+S / ⌘S）", "儲存所有已編輯的模組清單（Ctrl+S / ⌘S）", "編集した MOD リストをすべて保存（Ctrl+S / ⌘S）"));
    }
}
