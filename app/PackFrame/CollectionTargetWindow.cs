using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace PackFrame;

internal sealed class CollectionTargetWindow : Window
{
    public CollectionTargetWindow(IReadOnlyList<OwnedCollection> candidates, string language,
        IEnumerable<ulong>? desiredMods = null)
    {
        string T(string en, string hans, string hant, string ja) => OperationText.Choose(language, en, hans, hant, ja);
        Title = T("Choose Workshop collection", "选择创意工坊合集", "選擇工作坊合集", "Workshop コレクションの選択");
        Width = 680; Height = 540; MinWidth = 480; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = ThemeService.BackgroundBrush;
        Foreground = ThemeService.PrimaryTextBrush;
        var state = new CollectionTargetState(candidates, desiredMods);
        var grid = new Grid { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0,0,0,8) };
        header.Children.Add(new TextBlock { Text = T(
            "Select a collection you own to update, or create a separate collection.",
            "选择您拥有的合集进行更新，或创建独立合集。", "選擇您擁有的合集進行更新，或建立獨立合集。",
            "自分のコレクションを選んで更新するか、別のコレクションを作成します。"), TextWrapping = TextWrapping.Wrap });
        Button Make(string text) => new() { Content = text, MinHeight = 36, Padding = new Thickness(10,4),
            Theme = Application.Current?.FindResource("FlatButton") as ControlTheme };
        var search = new TextBox { PlaceholderText = T("Search by name or collection ID…", "搜索名称或合集 ID…",
            "搜尋名稱或合集 ID…", "名前またはコレクション ID を検索…"), MinHeight = 34,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        AutomationProperties.SetName(search, search.PlaceholderText?.ToString());
        var clear = Make("×"); clear.MinWidth = 34;
        var clearText = T("Clear search (Esc)", "清除搜索（Esc）", "清除搜尋（Esc）", "検索をクリア（Esc）");
        AutomationProperties.SetName(clear, clearText); ToolTip.SetTip(clear, clearText);
        var searchGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        searchGrid.Children.Add(search); Grid.SetColumn(clear,1); searchGrid.Children.Add(clear);
        header.Children.Add(new Border { Child = searchGrid, BorderThickness = new Thickness(1), BorderBrush = ThemeService.SecondaryBrush });
        var shortcut = OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";
        header.Children.Add(new TextBlock { Text = T($"{shortcut} search · ↓ browse · Esc clear", $"{shortcut} 搜索 · ↓ 浏览 · Esc 清除",
            $"{shortcut} 搜尋 · ↓ 瀏覽 · Esc 清除", $"{shortcut} 検索 · ↓ 一覧へ · Esc クリア"), FontSize = 12,
            TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.SecondaryTextBrush });
        var count = new TextBlock(); header.Children.Add(count); grid.Children.Add(header);
        var list = new ListBox
        {
            ItemsSource = state.Visible, Background = Brushes.Transparent, BorderThickness = new Thickness(1),
            BorderBrush = ThemeService.SecondaryBrush, SelectionMode = SelectionMode.Single,
            ItemTemplate = new FuncDataTemplate<OwnedCollection>((item, _) => new StackPanel
            {
                Spacing = 4, Margin = new Thickness(4,8), Children =
                {
                    new TextBlock { Text = item?.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
                        MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, [ToolTip.TipProperty] = item?.Title },
                    new TextBlock { Text = $"ID: {item?.Id} · " + T($"Mods: {item?.Children.Count}", $"{item?.Children.Count} 个模组",
                        $"{item?.Children.Count} 個模組", $"{item?.Children.Count} MOD"),
                        FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.SecondaryTextBrush }
                }
            })
        };
        AutomationProperties.SetName(list, Title);
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = {
            new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            new Setter(AutomationProperties.NameProperty, new Binding(".") { Converter = new FuncValueConverter<OwnedCollection, string>(
                item => $"{item?.Title} · ID: {item?.Id}") }) } });
        KeyboardNavigation.SetTabNavigation(list, KeyboardNavigationMode.Once);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        var empty = new TextBlock { Text = T("No owned collections are available. You can create a new collection.",
            "暂无可用的本人合集。您可以创建新合集。", "尚無可用的本人合集。您可以建立新合集。", "利用できる自分のコレクションがありません。新規作成できます。"),
            IsVisible = state.TotalCount == 0, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12,24),
            Foreground = ThemeService.SecondaryTextBrush };
        var listLayer = new Grid(); listLayer.Children.Add(list); listLayer.Children.Add(empty);
        Grid.SetRow(listLayer,1); grid.Children.Add(listLayer);
        var selection = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Make(Localization.CancelButton(language)); cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        var create = Make(T("Create new", "另建新合集", "另建新合集", "新規作成"));
        create.Click += (_, _) => Close((ulong?)0UL);
        var update = Make(T("Update selected", "更新所选合集", "更新所選合集", "選択を更新"));
        update.Click += (_, _) => { if (state.Selected is { } selected) Close((ulong?)selected.Id); };
        void UpdateSelection()
        {
            update.IsEnabled = state.Selected is not null;
            selection.Text = state.Selected is { } selected
                ? T($"Update collection ID: {selected.Id}", $"更新合集 ID：{selected.Id}", $"更新合集 ID：{selected.Id}", $"更新する ID: {selected.Id}") + "\n" +
                    (state.Changes is { } changes
                        ? T($"Mods: add {changes.Add} · remove {changes.Remove}", $"新增 {changes.Add} 个模组 · 移除 {changes.Remove} 个模组",
                            $"新增 {changes.Add} 個模組 · 移除 {changes.Remove} 個模組", $"追加 {changes.Add} MOD · 削除 {changes.Remove} MOD")
                        : T("The selected collection will be updated.", "将更新所选合集。", "將更新所選合集。", "選択したコレクションを更新します。"))
                : T("Select a collection to review the update. Create new leaves existing collections unchanged.",
                    "请选择合集以检查更新。另建新合集不会更改现有合集。", "請選擇合集以檢查更新。另建新合集不會變更現有合集。",
                    "更新するコレクションを選択してください。新規作成では既存のコレクションを変更しません。");
        }
        var filtering = false;
        list.SelectionChanged += (_, _) => { if (!filtering) { state.Select((list.SelectedItem as OwnedCollection)?.Id); UpdateSelection(); } };
        void Filter()
        {
            state.Filter(search.Text);
            filtering = true;
            try { list.ItemsSource = state.Visible; list.SelectedItem = state.Selected; }
            finally { filtering = false; }
            clear.IsEnabled = !string.IsNullOrEmpty(search.Text);
            empty.IsVisible = state.Visible.Count == 0;
            if (state.TotalCount > 0) empty.Text = T("No matching collections. Clear search to show all choices.",
                "没有匹配的合集。清除搜索以显示所有选项。", "沒有符合的合集。清除搜尋以顯示所有選項。", "一致するコレクションがありません。検索をクリアすると全件を表示します。");
            count.Text = T($"Showing {state.Visible.Count} / {state.TotalCount} collections", $"显示 {state.Visible.Count} / {state.TotalCount} 个合集",
                $"顯示 {state.Visible.Count} / {state.TotalCount} 個合集", $"{state.TotalCount} コレクション中 {state.Visible.Count} 件を表示");
            UpdateSelection();
        }
        search.TextChanged += (_, _) => Filter(); clear.Click += (_, _) => { search.Clear(); search.Focus(); };
        foreach (var button in new[] { cancel, create, update }) button.Margin = new Thickness(8,4,0,0);
        buttons.Children.Add(cancel); buttons.Children.Add(create); buttons.Children.Add(update);
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0,12,0,0) };
        footer.Children.Add(selection); footer.Children.Add(buttons);
        Grid.SetRow(footer,2); grid.Children.Add(footer); Content = grid;
        bool closed = false; Closed += (_, _) => closed = true;
        AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            { search.Focus(); search.SelectAll(); e.Handled = true; }
            else if (e.Key == Key.Escape && !string.IsNullOrEmpty(search.Text) && (search.IsKeyboardFocusWithin || list.IsKeyboardFocusWithin))
            { search.Clear(); search.Focus(); e.Handled = true; }
            else if (e.Key is Key.Down or Key.Enter && e.KeyModifiers == KeyModifiers.None && search.IsKeyboardFocusWithin)
            {
                if (list.ItemCount > 0)
                {
                    if (list.SelectedIndex < 0) list.SelectedIndex = 0;
                    var target = list.SelectedItem; list.ScrollIntoView(target!);
                    list.ContainerFromIndex(list.SelectedIndex)?.Focus(NavigationMethod.Directional);
                    Dispatcher.UIThread.Post(() => { if (!closed && search.IsKeyboardFocusWithin && ReferenceEquals(target,list.SelectedItem))
                        list.ContainerFromIndex(list.SelectedIndex)?.Focus(NavigationMethod.Directional); }, DispatcherPriority.Loaded);
                }
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        Filter();
        Opened += (_, _) => cancel.Focus();
    }
}
