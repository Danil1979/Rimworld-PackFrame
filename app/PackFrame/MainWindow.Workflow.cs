using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PackFrame;

public partial class MainWindow
{
    private long _lastSelectionAction;
    private readonly ObservableCollection<PendingInstanceRow> _pendingInstanceRows = new();
    private Flyout? _pendingInstancesFlyout;

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
            var guidance = PaneEmptyGuidance.Create(_config.Language, _gameInstallation is not null,
                _allMods.Count > 0, filtered, column > 0,
                _searchSources.TryGetValue(pane.List, out var source) ? source.Count : 0,
                column == 1 ? _missingMods1.Count : column == 2 ? _missingMods2.Count : 0,
                column == 0 || GetInstanceForColumn(column) is not null);
            Find<TextBlock>($"PaneEmptyTitle{column}").Text = guidance.Title;
            Find<TextBlock>($"PaneEmptyBody{column}").Text = guidance.Body;

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
        else UpdateHeaders();
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
        var rows = PendingInstanceSummary.Rows(pending, _instances);
        if (!_pendingInstanceRows.SequenceEqual(rows))
        {
            _pendingInstanceRows.Clear();
            foreach (var row in rows) _pendingInstanceRows.Add(row);
        }
        var text = PendingInstanceSummary.Caption(_config.Language, pending.Count);
        Find<Border>("EditStatusBanner").IsVisible = pending.Count > 0;
        if (pending.Count == 0) _pendingInstancesFlyout?.Hide();
        var review = Find<Button>("ReviewPendingInstancesButton");
        review.Content = PendingInstanceSummary.ReviewLabel(_config.Language) + " ▾";
        AutomationProperties.SetName(review, PendingInstanceSummary.ReviewLabel(_config.Language));
        AutomationProperties.SetHelpText(review, text);
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

    private void OnReviewPendingInstancesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || _pendingInstanceRows.Count == 0) return;
        _pendingInstancesFlyout?.Hide();
        var scroll = new ScrollViewer
        {
            Width = 460,
            MaxHeight = 300,
            Focusable = true,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new ItemsControl
            {
                ItemsSource = _pendingInstanceRows,
                ItemTemplate = (IDataTemplate)Resources["PendingInstanceTemplate"]!
            }
        };
        AutomationProperties.SetName(scroll, PendingInstanceSummary.ReviewLabel(_config.Language));
        _pendingInstancesFlyout = new Flyout { Content = scroll };
        _pendingInstancesFlyout.Opened += (_, _) => scroll.Focus();
        _pendingInstancesFlyout.ShowAt(button);
        e.Handled = true;
    }
}
