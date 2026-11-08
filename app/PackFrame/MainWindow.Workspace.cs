using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class MainWindow
{
    // Keep the familiar RimPy ordering: details, available mods, then load order.
    private const int DetailsColumnIndex = 0;
    private bool _workspaceReady;
    private bool _applyingWorkspaceLayout;
    private bool _detailsVisible = true;
    private bool _gameFolderUserChoice;
    private GridLength _detailsWidth = new(300);

    private void InitializeWorkspace()
    {
        // Fluent's default expander text/chevron assume a dark background. These
        // shared adaptive brushes also update when the user changes their theme.
        foreach (var part in new[] { "Header", "Chevron" })
        foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
            Resources[$"Expander{part}Foreground{state}"] = ThemeService.PrimaryTextBrush;
        _workspaceReady = true;
        Find<Grid>("WorkspaceGrid").SizeChanged += (_, _) => ApplyWorkspaceLayout();
        ApplyWorkspaceLayout();
        UpdateWorkspaceChrome();
    }

    private void OnModDetailsToggleClick(object? sender, RoutedEventArgs e)
    {
        _detailsVisible = !Find<Border>("DetailsPanel").IsVisible;
        ApplyWorkspaceLayout();
    }

    private void ApplyWorkspaceLayout()
    {
        if (!_workspaceReady || _applyingWorkspaceLayout) return;
        _applyingWorkspaceLayout = true;
        try
        {
            var grid = Find<Grid>("WorkspaceGrid");
            var panel = Find<Border>("DetailsPanel");
            // Only hide the view. Selection, preview and independent pane state remain intact.
            // Comparison keeps the familiar left inspector in place. Reclaiming
            // its width is an explicit choice through the Mod details toggle.
            var visible = _detailsVisible;
            // The splitter owns the live width while visible. Capture it on every layout
            // refresh, including a window resize, instead of restoring the initial width.
            if (panel.IsVisible) _detailsWidth = grid.ColumnDefinitions[DetailsColumnIndex].Width;
            var splitter = Find<GridSplitter>("DetailsSplitter");
            var restoreFocus = !visible && (panel.IsKeyboardFocusWithin || splitter.IsKeyboardFocusWithin);
            panel.IsVisible = visible;
            splitter.IsVisible = visible;
            grid.ColumnDefinitions[1].Width = new GridLength(visible ? 12 : 0);
            var workspaceWidth = grid.Bounds.Width > 0 ? grid.Bounds.Width : System.Math.Max(0, ClientSize.Width - 24);
            var managerMinimum = _config.ShowSecondaryPane ? 3 * 248 + 16 : 630;
            grid.ColumnDefinitions[2].MinWidth = managerMinimum;
            var detailsColumn = grid.ColumnDefinitions[DetailsColumnIndex];
            detailsColumn.MinWidth = visible ? 240 : 0;
            // Constrain actual size without replacing the requested Width. A user's
            // wider preference therefore returns when the workspace grows again.
            detailsColumn.MaxWidth = System.Math.Clamp(workspaceWidth - managerMinimum - 12, 240, 420);
            detailsColumn.Width = visible ? _detailsWidth : new GridLength(0);
            var toggle = Find<Button>("ModDetailsToggle");
            toggle.Content = (visible ? "▣  " : "□  ") + WorkflowText("Mod details", "模组详情", "模組詳情", "MOD 詳細");
            AutomationProperties.SetName(toggle, WorkflowText(visible ? "Hide mod details" : "Show mod details",
                visible ? "隐藏模组详情" : "显示模组详情", visible ? "隱藏模組詳情" : "顯示模組詳情", visible ? "MOD 詳細を隠す" : "MOD 詳細を表示"));
            if (restoreFocus) toggle.Focus();
        }
        finally { _applyingWorkspaceLayout = false; }
    }

    private void OnGameFolderToggleClick(object? sender, RoutedEventArgs e)
    {
        _gameFolderUserChoice = true;
        var panel = Find<Border>("GameFolderPanel");
        panel.IsVisible = !panel.IsVisible;
        UpdateGameFolderToggle();
        if (panel.IsVisible) _pathBox.Focus();
    }

    private void UpdateGameFolderToggle()
    {
        var button = Find<Button>("GameFolderToggle");
        button.Content = WorkflowText("Game folder", "游戏目录", "遊戲目錄", "ゲームフォルダー") +
            (Find<Border>("GameFolderPanel").IsVisible ? " ▴" : " ▾");
        ToolTip.SetTip(button, _pathBox.Text);
    }

    private void UpdateWorkspaceChrome()
    {
        if (!_workspaceReady) return;
        if (_gameInstallation is null) Find<Border>("GameFolderPanel").IsVisible = true;
        else if (!_gameFolderUserChoice)
        {
            var panel = Find<Border>("GameFolderPanel");
            var restoreFocus = panel.IsKeyboardFocusWithin;
            panel.IsVisible = false;
            if (restoreFocus) Find<Button>("GameFolderToggle").Focus();
        }
        UpdateGameFolderToggle();
        Find<TextBlock>("AvailableHeading").Text = WorkflowText("Available mods", "可用模组", "可用模組", "利用可能な MOD");
        UpdateAvailableTargetGuidance();
        foreach (var column in new[] { 1, 2 })
            Find<TextBlock>($"LoadOrderHeading{column}").Text = WorkflowText("Load order", "加载顺序", "載入順序", "読み込み順序");
        Find<TextBlock>("DetailsHeading").Text = WorkflowText("MOD DETAILS", "模组详情", "模組詳情", "MOD 詳細");
        Find<Expander>("TechnicalDetails").Header = WorkflowText("Package & location", "标识与位置", "識別與位置", "パッケージと保存場所");
        ApplyWorkspaceLayout();
    }
    private string PaneCountCaption(int column, int count)
    {
        var number = PaneCount(column, count);
        return column == 0
            ? WorkflowText($"{number} available", $"{number} 个可用", $"{number} 個可用", $"{number} 利用可能")
            : WorkflowText($"{number} enabled", $"{number} 个已启用", $"{number} 個已啟用", $"{number} 有効");
    }

}
