using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MainWindow
{
    private void UpdateWorkspaceGuidance()
    {
        var language = _config.Language;
        var needsGame = _gameInstallation is null;
        Find<TextBlock>("DetailsEmptyTitle").Text = needsGame
            ? OperationText.Choose(language, "Set up your library", "设置模组库", "設定模組庫", "MOD ライブラリの設定")
            : OperationText.Choose(language, "Build your mod list", "编辑模组列表", "編輯模組清單", "MOD リストの編集");
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
            "1  Choose an instance above the active list.\n\n2  Select mods in Inactive, then use Enable above the destination list. Disable acts on its own list. Drag to reorder.\n\n3  Use Save changes at the bottom right (Ctrl+S / ⌘S), then launch.",
            "1  在启用列表上方选择实例。\n\n2  在未启用列表中选择模组，点击目标列表上方的“启用”。“禁用”只作用于所在列表。拖动调整顺序。\n\n3  点击右下角“保存修改”（Ctrl+S / ⌘S），然后启动游戏。",
            "1  在啟用清單上方選擇實例。\n\n2  在未啟用清單中選擇模組，點選目標清單上方的「啟用」。「停用」只作用於所在清單。拖曳調整順序。\n\n3  點選右下角「儲存修改」（Ctrl+S / ⌘S），然後啟動遊戲。",
            "1  有効なリストの上でインスタンスを選択。\n\n2  無効リストで選択し、移動先の上の「有効化」を使用。「無効化」はそのリストに適用。ドラッグで順序を変更。\n\n3  右下の「変更を保存」（Ctrl+S / ⌘S）で保存後、ゲームを起動。");
        Find<TextBlock>("DetailsSelectionHint").Text = OperationText.Choose(language,
            "Open on right adds another view for comparing or copying between instances.",
            "“在右侧打开”可添加另一视图，用于比较或在实例间复制模组。",
            "「在右側開啟」可新增另一個檢視，用於比較或在實例間複製模組。",
            "「右側で開く」で別のビューを追加し、インスタンス間で比較やコピーができます。");
        Find<TextBlock>("InactiveActionHint").Text = WorkflowText(
            "Select here; Enable above the target list.\nEsc clears search and filters.",
            "在此选择，在目标列表上方启用。\nEsc 清除搜索和筛选。",
            "在此選擇，在目標清單上方啟用。\nEsc 清除搜尋和篩選。",
            "ここで選択し、移動先の上で有効化。\nEsc で検索とフィルターを解除。");
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
            AutomationProperties.SetName(button, actions);
        }
        foreach (var search in new[] { _inactiveSearchBox, _activeSearchBox1, _activeSearchBox2 })
            AutomationProperties.SetName(search, Localization.SearchMods(language));
        for (var column = 0; column < 3; column++)
            Find<TextBlock>($"SearchShortcutHint{column}").Text = System.OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";
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
