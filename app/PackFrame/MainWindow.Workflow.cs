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
        !string.IsNullOrWhiteSpace(search.Query) || search.SourceIndex != 0 ||
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
            var reset = Find<Button>($"ResetFilters{column}");
            reset.Content = WorkflowText("Reset", "重置", "重設", "リセット");
            reset.IsEnabled = filtered;
            var resetHint = WorkflowText("Clear this pane's search, source and status filters.",
                "清除此栏的搜索、来源及状态筛选。", "清除此欄的搜尋、來源及狀態篩選。", "このペインの検索、ソース、状態フィルターを解除します。");
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
                    ? WorkflowText("Try another search, or clear this pane's filters. Your mod list has not been changed.", "尝试其他搜索，或清除此栏的筛选。模组列表未被修改。", "嘗試其他搜尋，或清除此欄的篩選。模組清單未被修改。", "別の検索を試すか、このペインのフィルターを解除してください。MOD リストは変更されていません。")
                    : column == 0
                        ? WorkflowText("Every available mod is already active in the selected instance.", "所选实例已启用所有可用模组。", "所選實例已啟用所有可用模組。", "選択中のインスタンスでは、利用できる MOD がすべて有効です。")
                        : WorkflowText("Select mods in Inactive, then choose Enable selected to build this list.", "在未启用列表中选择模组，然后点击“启用所选”以添加。", "在未啟用清單中選擇模組，然後點選「啟用所選」以新增。", "無効リストで MOD を選び、「選択を有効化」で追加します。");
            var emptyReset = Find<Button>($"PaneEmptyReset{column}");
            emptyReset.Content = WorkflowText("Clear filters", "清除筛选", "清除篩選", "フィルターを解除");
            emptyReset.IsVisible = filtered;
        }
        UpdateSelectionAction();
    }

    private void OnResetPaneFiltersClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var column)) return;
        var pane = PaneWorkflow(column);
        SetPaneInteractionContext(pane.List);
        pane.State.Query = string.Empty;
        pane.State.SourceIndex = 0;
        pane.Filter.Sort = pane.Filter.Version = pane.Filter.Duplicate = pane.Filter.Local = ProblemFilterMode.Off;
        pane.Search.Text = string.Empty;
        UpdateSourceButton(pane.Source, pane.State, _config.Language);
        if (_allMods.Count > 0) RefreshSearch(pane.List);
        else UpdatePaneWorkflow();
        pane.Search.Focus();
        e.Handled = true;
    }

    private void UpdateSelectionAction()
    {
        var list = GetCommandList();
        var count = list?.SelectedItems?.Count ?? 0;
        var enabling = list is null || ReferenceEquals(list, _inactiveList);
        var instance = enabling ? GetSelectedInstance(_lastEnableColumn - 1) : GetInstanceForColumn(GetListColumn(list!));
        var action = Find<Button>("SelectionActionButton");
        action.Content = enabling
            ? WorkflowText("Enable selected", "启用所选", "啟用所選", "選択を有効化")
            : WorkflowText("Disable selected", "禁用所选", "停用所選", "選択を無効化");
        action.IsEnabled = count > 0 && instance is not null && !instance.IsDamaged && !_loadingMods && !_painting && !_customDragActive;
        var summary = count == 0
            ? WorkflowText("Select mods in a list", "在列表中选择模组", "在清單中選擇模組", "リストで MOD を選択")
            : WorkflowText($"{count} selected · {instance?.DisplayName}", $"已选择 {count} 个 · {instance?.DisplayName}", $"已選擇 {count} 個 · {instance?.DisplayName}", $"{count} 件選択 · {instance?.DisplayName}");
        Find<TextBlock>("SelectionSummary").Text = summary;
        ToolTip.SetTip(action, $"{action.Content}: {summary}");
        AutomationProperties.SetName(action, $"{action.Content}: {summary}");
    }

    private void OnSelectionActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { IsEnabled: true } || _customDragActive || _painting || _loadingMods) return;
        var list = GetCommandList();
        if (list is null || !list.IsEffectivelyVisible) return;
        // A double click should not immediately undo itself as selection moves to the other pane.
        var now = Environment.TickCount64;
        if (now - _lastSelectionAction < 400) return;
        _lastSelectionAction = now;
        MoveModsOnDoubleClick(list);
        UpdateSelectionAction();
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
        var status = Find<TextBlock>("EditStatusText");
        status.Text = text;
        status.FontWeight = pending.Count > 0 ? FontWeight.SemiBold : FontWeight.Normal;
        ToolTip.SetTip(status, text);
        ToolTip.SetTip(_saveButton, WorkflowText("Save all edited mod lists (Ctrl+S / ⌘S)",
            "保存所有已编辑的模组列表（Ctrl+S / ⌘S）", "儲存所有已編輯的模組清單（Ctrl+S / ⌘S）", "編集した MOD リストをすべて保存（Ctrl+S / ⌘S）"));
    }
}
