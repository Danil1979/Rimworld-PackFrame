using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

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
        MinWidth = 480; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = ThemeService.BackgroundBrush;
        Foreground = ThemeService.PrimaryTextBrush;
        var theme = Application.Current?.FindResource("FlatButton") as ControlTheme;
        Button Button(string label) => new() { Content = label, Theme = theme, MinHeight = 32, Padding = new Thickness(8, 4) };
        TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.PrimaryTextBrush };
        var state = new UpdateChoiceState(plan.Conflicts, language);
        var summary = Text(string.Empty);
        var count = Text(string.Empty);
        var empty = Text(T("No matching files. Clear search to show all choices.", "没有匹配的文件。清除搜索以显示所有选项。",
            "沒有符合的檔案。清除搜尋以顯示所有選項。", "一致するファイルがありません。検索をクリアすると全件を表示します。"));
        empty.Margin = new Thickness(12);
        var orderText = T("Keep my order (new mods may not be enabled)", "保留当前排序（新增模组可能未启用）",
            "保留目前排序（新增模組可能未啟用）", "現在の並び順を保持（新 MOD は未有効の場合あり）");
        var order = new CheckBox { Content = Text(orderText), IsVisible = plan.OrderChanged,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(order, orderText);
        var keepAll = Button(T("Keep all local", "全部保留本地", "全部保留本機", "すべてローカル版を保持"));
        var useAll = Button(T("Use update for all", "全部使用更新", "全部使用更新", "すべて更新版を使用"));
        var allHint = T("Applies to all conflicting files, including hidden search results.", "应用于所有冲突文件，包括搜索隐藏的文件。",
            "套用至所有衝突檔案，包括搜尋隱藏的檔案。", "検索で非表示の項目を含む、すべての競合ファイルが対象です。");
        foreach (var button in new[] { keepAll, useAll }) { ToolTip.SetTip(button, allHint); AutomationProperties.SetHelpText(button, allHint); }
        bool changingAll = false;
        void UpdateSummary()
        {
            if (changingAll) return;
            var incoming = state.UseUpdateCount;
            var kept = state.Rows.Count - incoming;
            summary.Text = T($"Files: {incoming} use update · {kept} keep local", $"文件：{incoming} 个使用更新 · {kept} 个保留本地",
                $"檔案：{incoming} 個使用更新 · {kept} 個保留本機", $"ファイル: 更新 {incoming} 件 · ローカル保持 {kept} 件") + "\n" +
                (order.IsChecked == true
                    ? T("Load order: keep current", "加载顺序：保留当前", "載入順序：保留目前", "読み込み順: 現在の順序を保持")
                    : T("Load order: use update", "加载顺序：使用更新", "載入順序：使用更新", "読み込み順: 更新版を使用"));
            keepAll.IsEnabled = incoming > 0;
            useAll.IsEnabled = kept > 0;
        }
        foreach (var row in state.Rows) row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(UpdateChoiceRow.UseUpdate)) UpdateSummary(); };
        order.IsCheckedChanged += (_, _) => UpdateSummary();
        void SetAll(bool useUpdate)
        {
            changingAll = true;
            try { state.SetAll(useUpdate); }
            finally { changingAll = false; UpdateSummary(); }
        }
        keepAll.Click += (_, _) => SetAll(false);
        useAll.Click += (_, _) => SetAll(true);
        var search = new TextBox { PlaceholderText = T("Search file paths…", "搜索文件路径…", "搜尋檔案路徑…", "ファイルパスを検索…"),
            MinHeight = 32, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 4) };
        AutomationProperties.SetName(search, search.PlaceholderText?.ToString());
        var clear = Button("×"); clear.MinWidth = 32;
        var clearText = T("Clear search (Esc)", "清除搜索（Esc）", "清除搜尋（Esc）", "検索をクリア（Esc）");
        AutomationProperties.SetName(clear, clearText); ToolTip.SetTip(clear, clearText);
        var searchGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        searchGrid.Children.Add(search); Grid.SetColumn(clear, 1); searchGrid.Children.Add(clear);
        var searchBorder = new Border { BorderBrush = ThemeService.SecondaryBrush, BorderThickness = new Thickness(1), Child = searchGrid };
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            SelectionMode = SelectionMode.Single, ItemsSource = state.Rows };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        KeyboardNavigation.SetTabNavigation(list, KeyboardNavigationMode.Once);
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(AutomationProperties.NameProperty, new Binding(nameof(UpdateChoiceRow.AccessibleName)))
            }
        });
        list.ItemTemplate = new FuncDataTemplate<UpdateChoiceRow>((row, _) =>
        {
            if (row is null) return new Border();
            var check = new CheckBox { Content = Text(row.Path), Focusable = false, Padding = new Thickness(8, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            check.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(UpdateChoiceRow.UseUpdate)) { Mode = BindingMode.TwoWay });
            check.Bind(AutomationProperties.NameProperty, new Binding(nameof(UpdateChoiceRow.AccessibleName)));
            return new Border { BorderBrush = ThemeService.SecondaryBrush, BorderThickness = new Thickness(0, 0, 0, 1), Child = check };
        });
        var listLayer = new Grid(); listLayer.Children.Add(list); listLayer.Children.Add(empty);
        var listBorder = new Border { BorderBrush = ThemeService.SecondaryBrush, BorderThickness = new Thickness(1), Child = listLayer };
        void Filter()
        {
            var prior = list.SelectedItem;
            var visible = state.Filter(search.Text);
            list.ItemsSource = visible;
            list.SelectedItem = prior is UpdateChoiceRow row && visible.Contains(row) ? row : null;
            clear.IsEnabled = !string.IsNullOrEmpty(search.Text);
            empty.IsVisible = visible.Count == 0;
            count.Text = T($"Showing {visible.Count} / {state.Rows.Count} files", $"显示 {visible.Count} / {state.Rows.Count} 个文件",
                $"顯示 {visible.Count} / {state.Rows.Count} 個檔案", $"{state.Rows.Count} ファイル中 {visible.Count} 件を表示");
        }
        search.TextChanged += (_, _) => Filter();
        clear.Click += (_, _) => { search.Clear(); search.Focus(); };
        var filesHeader = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 6) };
        filesHeader.Children.Add(Text(T("Checked files use the update, which may replace or remove local content.",
            "勾选文件使用更新，可能替换或移除本地内容。", "勾選檔案使用更新，可能取代或移除本機內容。",
            "チェックしたファイルは更新版を使用し、ローカルの内容が置換または削除される場合があります。")));
        filesHeader.Children.Add(searchBorder);
        var shortcut = OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";
        filesHeader.Children.Add(Text(T($"{shortcut} search · ↓ browse · Space toggle · Esc clear", $"{shortcut} 搜索 · ↓ 浏览 · 空格切换 · Esc 清除",
            $"{shortcut} 搜尋 · ↓ 瀏覽 · 空白鍵切換 · Esc 清除", $"{shortcut} 検索 · ↓ 一覧へ · Space 切替 · Esc クリア")));
        var bulk = new WrapPanel { Orientation = Orientation.Horizontal };
        keepAll.Margin = new Thickness(0, 0, 8, 4); useAll.Margin = new Thickness(0, 0, 0, 4);
        bulk.Children.Add(keepAll); bulk.Children.Add(useAll); filesHeader.Children.Add(bulk);
        var files = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        files.Children.Add(filesHeader); Grid.SetRow(listBorder, 1); files.Children.Add(listBorder);
        Grid.SetRow(count, 2); count.Margin = new Thickness(0, 4, 0, 0); files.Children.Add(count);
        var modPanel = new StackPanel { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        modPanel.Children.Add(order);
        modPanel.Children.Add(Text(plan.OrderChanged
            ? T("If unchecked, the update's mod list and load order will replace yours.", "若不勾选，更新中的模组列表和加载顺序将替换当前列表。",
                "若不勾選，更新中的模組清單與載入順序將取代目前清單。", "チェックを外すと、更新の MOD リストと読み込み順で現在のリストを置き換えます。")
            : T("The update's mod list and load order will be applied.", "将应用更新中的模组列表和加载顺序。", "將套用更新中的模組清單與載入順序。", "更新の MOD リストと読み込み順を適用します。")));
        modPanel.Children.Add(Text(T($"Added mods: {plan.Added.Count} · Removed mods: {plan.Removed.Count}",
            $"新增模组：{plan.Added.Count} · 移除模组：{plan.Removed.Count}", $"新增模組：{plan.Added.Count} · 移除模組：{plan.Removed.Count}",
            $"追加 MOD: {plan.Added.Count} · 削除 MOD: {plan.Removed.Count}")));
        modPanel.Children.Add(new SelectableTextBlock { Text = string.Join("\n", plan.Added.Select(id => "+ " + id).Concat(plan.Removed.Select(id => "- " + id))),
            TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.PrimaryTextBrush });
        var fileTab = new TabItem { FontSize = 16, Header = T($"Local files ({state.Rows.Count})", $"本地文件（{state.Rows.Count}）", $"本機檔案（{state.Rows.Count}）", $"ローカルファイル ({state.Rows.Count})"), Content = files, IsVisible = state.Rows.Count > 0 };
        var orderTab = new TabItem { FontSize = 16, Header = T($"Mods & order (+{plan.Added.Count}/−{plan.Removed.Count})", $"模组与顺序（+{plan.Added.Count}/−{plan.Removed.Count}）",
                $"模組與順序（+{plan.Added.Count}/−{plan.Removed.Count}）", $"MOD と順序 (+{plan.Added.Count}/−{plan.Removed.Count})"),
            Content = new ScrollViewer { Content = modPanel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        var tabs = new TabControl { ItemsSource = new[] { fileTab, orderTab }, SelectedIndex = state.Rows.Count > 0 ? 0 : 1 };
        var cancel = Button(Localization.CancelButton(language)); cancel.IsCancel = true; cancel.Click += (_, _) => Close();
        var apply = Button(T("Apply update", "应用更新", "套用更新", "更新を適用"));
        apply.Click += (_, _) => Close(state.Confirm(order.IsChecked == true));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel); buttons.Children.Add(apply);
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(summary); footer.Children.Add(buttons);
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(16) };
        layout.Children.Add(tabs); Grid.SetRow(footer, 1); layout.Children.Add(footer); Content = layout;
        bool closed = false;
        Closed += (_, _) => closed = true;
        AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) && state.Rows.Count > 0)
            { tabs.SelectedItem = fileTab; search.Focus(); search.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Escape && !string.IsNullOrEmpty(search.Text) && (search.IsKeyboardFocusWithin || list.IsKeyboardFocusWithin))
            { search.Clear(); search.Focus(); e.Handled = true; }
            else if (e.Key is Key.Down or Key.Enter && search.IsKeyboardFocusWithin && list.ItemCount > 0)
            {
                if (list.SelectedIndex < 0) list.SelectedIndex = 0;
                var target = list.SelectedItem;
                list.ScrollIntoView(target!);
                list.ContainerFromIndex(list.SelectedIndex)?.Focus(NavigationMethod.Directional);
                Dispatcher.UIThread.Post(() =>
                {
                    if (!closed && search.IsKeyboardFocusWithin && ReferenceEquals(list.SelectedItem, target))
                        list.ContainerFromIndex(list.SelectedIndex)?.Focus(NavigationMethod.Directional);
                }, DispatcherPriority.Loaded);
                e.Handled = true;
            }
            else if (e.Key == Key.Space && list.IsKeyboardFocusWithin && list.SelectedItem is UpdateChoiceRow row)
            { row.UseUpdate = !row.UseUpdate; e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        Filter(); UpdateSummary(); Opened += (_, _) => cancel.Focus();
    }
}
