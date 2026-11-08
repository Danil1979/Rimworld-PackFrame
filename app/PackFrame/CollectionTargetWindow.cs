using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace PackFrame;

internal sealed class CollectionTargetWindow : Window
{
    public CollectionTargetWindow(IReadOnlyList<OwnedCollection> candidates, string language)
    {
        string T(string en, string hans, string hant, string ja) => OperationText.Choose(language, en, hans, hant, ja);
        Title = T("Choose Workshop collection", "选择创意工坊合集", "選擇工作坊合集", "Workshop コレクションの選択");
        Width = 680; Height = 440; MinWidth = 480; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = ThemeService.BackgroundBrush;
        Foreground = ThemeService.PrimaryTextBrush;
        var grid = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        grid.Children.Add(new TextBlock { Text = T(
            "Select a collection you own to update, or create a separate collection.",
            "选择您拥有的合集进行更新，或创建独立合集。", "選擇您擁有的合集進行更新，或建立獨立合集。",
            "自分のコレクションを選んで更新するか、別のコレクションを作成します。"), TextWrapping = TextWrapping.Wrap });
        var list = new ListBox
        {
            ItemsSource = candidates, Margin = new Thickness(0,16,0,16),
            ItemTemplate = new FuncDataTemplate<OwnedCollection>((item, _) => new StackPanel
            {
                Spacing = 4, Margin = new Thickness(4,8), Children =
                {
                    new TextBlock { Text = item?.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = $"steamcommunity.com/sharedfiles/filedetails/?id={item?.Id}",
                        FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = ThemeService.SecondaryTextBrush }
                }
            })
        };
        AutomationProperties.SetName(list, Title);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        Grid.SetRow(list, 1); grid.Children.Add(list);
        var empty = new TextBlock { Text = T("No owned collections are available. You can create a new collection.",
            "暂无可用的本人合集。您可以创建新合集。", "尚無可用的本人合集。您可以建立新合集。", "利用できる自分のコレクションがありません。新規作成できます。"),
            IsVisible = candidates.Count == 0, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,24),
            Foreground = ThemeService.SecondaryTextBrush };
        Grid.SetRow(empty,1); grid.Children.Add(empty);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        Button Make(string text) => new() { Content = text, MinHeight = 36, Margin = new Thickness(8,4,0,0),
            Theme = Application.Current?.FindResource("FlatButton") as ControlTheme };
        var cancel = Make(Localization.CancelButton(language)); cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        var create = Make(T("Create new", "另建新合集", "另建新合集", "新規作成"));
        create.Click += (_, _) => Close((ulong?)0UL);
        var update = Make(T("Update selected", "更新所选合集", "更新所選合集", "選択を更新"));
        update.IsEnabled = candidates.Count > 0;
        update.Click += (_, _) => { if (list.SelectedItem is OwnedCollection selected) Close((ulong?)selected.Id); };
        list.SelectionChanged += (_, _) => update.IsEnabled = list.SelectedItem is OwnedCollection;
        list.SelectedIndex = candidates.Count > 0 ? 0 : -1;
        buttons.Children.Add(cancel); buttons.Children.Add(create); buttons.Children.Add(update);
        Grid.SetRow(buttons,2); grid.Children.Add(buttons); Content = grid;
        Opened += (_, _) => cancel.Focus();
    }
}
