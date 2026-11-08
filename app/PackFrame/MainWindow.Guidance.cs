using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MainWindow
{
    private void UpdateWorkspaceGuidance()
    {
        UpdateWorkspaceChrome();
        var language = _config.Language;
        var needsGame = _gameInstallation is null;
        Find<TextBlock>("DetailsEmptyTitle").Text = needsGame
            ? OperationText.Choose(language, "Set up your library", "设置模组库", "設定模組庫", "MOD ライブラリの設定")
            : OperationText.Choose(language, "Select a mod", "选择模组", "選擇模組", "MOD を選択");
        Find<TextBlock>("DetailsEmptyBody").Text = needsGame
            ? OperationText.Choose(language,
                "Choose your RimWorld folder above. Use Auto Detect, or Browse to select it yourself.",
                "在上方选择 RimWorld 文件夹。可使用自动检测，或点击浏览手动选择。",
                "在上方選擇 RimWorld 資料夾。可使用自動偵測，或點選瀏覽手動選擇。",
                "上部で RimWorld フォルダーを選択します。自動検出するか、参照から指定してください。")
            : OperationText.Choose(language,
                "Select a mod to see its author, location and description here.",
                "选择模组，在此查看作者、位置和简介。",
                "選擇模組，在此檢視作者、位置和簡介。",
                "MOD を選択すると、作者、保存場所、説明がここに表示されます。");
        Find<TextBlock>("DetailsWorkflow").Text = OperationText.Choose(language,
            "1  Choose an instance above the active list.\n\n2  Select mods in Available mods, then use Enable above the destination list. Disable acts on its own list. Drag to reorder.\n\n3  Use Save changes at the bottom right (Ctrl+S / ⌘S), then launch.",
            "1  在启用列表上方选择实例。\n\n2  在“可用模组”中选择模组，点击目标列表上方的“启用”。“禁用”只作用于所在列表。拖动调整顺序。\n\n3  点击右下角“保存修改”（Ctrl+S / ⌘S），然后启动游戏。",
            "1  在啟用清單上方選擇實例。\n\n2  在「可用模組」中選擇模組，點選目標清單上方的「啟用」。「停用」只作用於所在清單。拖曳調整順序。\n\n3  點選右下角「儲存修改」（Ctrl+S / ⌘S），然後啟動遊戲。",
            "1  有効なリストの上でインスタンスを選択。\n\n2  「利用可能な MOD」で選択し、移動先の上の「有効化」を使用。「無効化」はそのリストに適用。ドラッグで順序を変更。\n\n3  右下の「変更を保存」（Ctrl+S / ⌘S）で保存後、ゲームを起動。");
        Find<TextBlock>("DetailsSelectionHint").Text = OperationText.Choose(language,
            "Compare adds another view for comparing or copying between instances.",
            "“对比”可添加另一视图，用于比较或在实例间复制模组。",
            "「比較」可新增另一個檢視，用於比較或在實例間複製模組。",
            "「比較」で別のビューを追加し、インスタンス間で比較やコピーができます。");
        Find<TextBlock>("InactiveActionHint").Text = WorkflowText(
            "Select mods, then Enable →",
            "选择模组，然后在右侧启用 →",
            "選擇模組，然後在右側啟用 →",
            "MOD を選択して有効化 →");
        var resize = OperationText.Choose(language,
            "Resize mod details. Drag, or use the arrow keys when focused.",
            "调整详情宽度：拖动分隔线，或聚焦后使用方向键。",
            "調整詳細資訊寬度：拖曳分隔線，或聚焦後使用方向鍵。",
            "詳細の幅を変更。ドラッグするか、フォーカスして矢印キーを使用します。");
        var splitter = Find<GridSplitter>("DetailsSplitter");
        ToolTip.SetTip(splitter, resize);
        AutomationProperties.SetName(splitter, resize);
        AutomationProperties.SetName(_pathBox, Localization.GamePathLabel(language));
        foreach (var button in new[] { _instanceActionButton1, _instanceActionButton2 })
        {
            var actions = OperationText.Choose(language, "Manage instance", "管理实例", "管理實例", "インスタンスの管理");
            button.Content = WorkflowText("Instance ▾", "实例 ▾", "實例 ▾", "管理 ▾");
            ToolTip.SetTip(button, actions);
        }
        var searchHelp = OperationText.Choose(language,
            "Down or Enter moves to matching mods without enabling or disabling them. Esc clears search and filters. In the list, Shift+F10 opens mod actions.",
            "按向下键或 Enter 移至匹配的模组，不会启用或禁用模组。Esc 清除搜索和筛选。在列表中按 Shift+F10 打开模组操作。",
            "按向下鍵或 Enter 移至符合的模組，不會啟用或停用模組。Esc 清除搜尋和篩選。在清單中按 Shift+F10 開啟模組操作。",
            "下矢印または Enter で一致する MOD に移動します。有効・無効は変更しません。Esc で検索とフィルターを解除。リスト内で Shift+F10 を押すと MOD 操作を開きます。");
        var listHelp = OperationText.Choose(language,
            "Arrow keys select mods; Shift+Up/Down extends the selection. Enter enables or disables the selection. Shift+F10 opens mod actions; Esc closes them. Ctrl+F / ⌘F searches this pane.",
            "方向键选择模组；Shift+上/下扩展选择。Enter 启用或禁用所选模组。Shift+F10 打开模组操作；Esc 关闭菜单。Ctrl+F / ⌘F 搜索此栏。",
            "方向鍵選擇模組；Shift+上/下擴展選取。Enter 啟用或停用所選模組。Shift+F10 開啟模組操作；Esc 關閉選單。Ctrl+F / ⌘F 搜尋此欄。",
            "矢印キーで選択し、Shift+上/下で選択範囲を拡張します。Enter で選択 MOD を有効・無効にします。Shift+F10 で MOD 操作を開き、Esc で閉じます。Ctrl+F / ⌘F でこのペインを検索。");
        for (var column = 0; column < 3; column++)
        {
            var pane = PaneWorkflow(column);
            AutomationProperties.SetHelpText(pane.Search, searchHelp);
            AutomationProperties.SetHelpText(pane.List, listHelp);
            var hint = Find<TextBlock>($"SearchShortcutHint{column}");
            hint.Text = System.OperatingSystem.IsMacOS() ? "⌘F · ↓" : "Ctrl+F · ↓";
            hint.IsHitTestVisible = true;
            ToolTip.SetTip(hint, searchHelp);
        }
        UpdatePaneAccessibility();
    }

    private void UpdatePaneAccessibility()
    {
        for (var column = 0; column < 3; column++)
        {
            var pane = PaneWorkflow(column);
            var field = column == 0 ? _inactiveSearchField : column == 1 ? _activeSearchField1 : _activeSearchField2;
            var instance = column == 0 ? null : GetSelectedInstance(column - 1);
            PaneAccessibility.Create(_config.Language, column, instance?.DisplayName).Apply(
                pane.List, pane.Search, field,
                column == 0 ? null : column == 1 ? _instanceCombo1 : _instanceCombo2,
                column == 0 ? null : column == 1 ? _instanceActionButton1 : _instanceActionButton2);
        }
    }

    private string SummaryCaption(TextBlock label)
    {
        var language = _config.Language;
        if (label == _activeProblemS1 || label == _activeProblemS2)
            return OperationText.Choose(language, "Order", "排序", "排序", "順序");
        if (label == _inactiveProblemV || label == _activeProblemV1 || label == _activeProblemV2)
            return OperationText.Choose(language, "Version", "版本", "版本", "バージョン");
        if (label == _inactiveProblemR || label == _activeProblemR1 || label == _activeProblemR2)
            return OperationText.Choose(language, "Duplicate", "重复", "重複", "重複");
        return OperationText.Choose(language, "Local", "本地", "本機", "ローカル");
    }

    private void UpdateSummaryAccessibility(TextBlock label, ProblemFilterMode mode)
    {
        if (label.Parent is not Button button) return;
        button.Classes.Set("filtered", mode != ProblemFilterMode.Off);
        // Keep active filters reachable even when their result is empty.
        var countText = label.Text?.Split(' ')[^1];
        button.IsVisible = mode != ProblemFilterMode.Off || !int.TryParse(countText, out var count) || count != 0;
        var state = mode switch
        {
            ProblemFilterMode.Hide => OperationText.Choose(_config.Language, "Hidden", "已排除", "已排除", "非表示"),
            ProblemFilterMode.Only => OperationText.Choose(_config.Language, "Only these", "仅显示此类", "僅顯示此類", "これのみ表示"),
            _ => OperationText.Choose(_config.Language, "All shown", "全部显示", "全部顯示", "すべて表示")
        };
        var help = OperationText.Choose(_config.Language,
            "Activate to cycle: hide, show only, show all.",
            "点击切换：排除、仅显示、全部显示。",
            "點選切換：排除、僅顯示、全部顯示。",
            "押すたびに、非表示・これのみ・すべて表示を切り替えます。");
        AutomationProperties.SetName(button, $"{label.Text}. {state}");
        AutomationProperties.SetHelpText(button, help);
        // Problem summaries retain their existing detailed hover popup.
        if (label == _inactiveLocalCount || label == _activeLocalCount1 || label == _activeLocalCount2)
            ToolTip.SetTip(button, $"{SummaryCaption(label)}. {state}. {help}");
    }

    private void OnSummaryFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: TextBlock label }) return;
        CloseProblemTooltip();
        if (label == _inactiveProblemV) _inactiveFilter.Version = CycleProblemFilter(_inactiveFilter.Version);
        else if (label == _inactiveProblemR) _inactiveFilter.Duplicate = CycleProblemFilter(_inactiveFilter.Duplicate);
        else if (label == _inactiveLocalCount) _inactiveFilter.Local = CycleProblemFilter(_inactiveFilter.Local);
        else if (label == _activeProblemS1) _activeFilter1.Sort = CycleProblemFilter(_activeFilter1.Sort);
        else if (label == _activeProblemV1) _activeFilter1.Version = CycleProblemFilter(_activeFilter1.Version);
        else if (label == _activeProblemR1) _activeFilter1.Duplicate = CycleProblemFilter(_activeFilter1.Duplicate);
        else if (label == _activeLocalCount1) _activeFilter1.Local = CycleProblemFilter(_activeFilter1.Local);
        else if (label == _activeProblemS2) _activeFilter2.Sort = CycleProblemFilter(_activeFilter2.Sort);
        else if (label == _activeProblemV2) _activeFilter2.Version = CycleProblemFilter(_activeFilter2.Version);
        else if (label == _activeProblemR2) _activeFilter2.Duplicate = CycleProblemFilter(_activeFilter2.Duplicate);
        else if (label == _activeLocalCount2) _activeFilter2.Local = CycleProblemFilter(_activeFilter2.Local);
        else return;
        RefreshManager(false);
        e.Handled = true;
    }
}
