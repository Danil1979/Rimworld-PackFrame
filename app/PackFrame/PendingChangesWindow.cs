using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace PackFrame;

internal enum PendingChangesChoice { Cancel, Save, Discard }

internal sealed class PendingChangesWindow : Window
{
    public PendingChangesWindow(string? language)
    {
        Title = "Rimworld PackFrame";
        RequestedThemeVariant = ThemeVariant.Dark;
        Background = ThemeService.BackgroundBrush;
        Foreground = ThemeService.PrimaryTextBrush;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right };
        Button? cancelButton = null;
        foreach (var (text, choice) in new[]
        {
            (Localization.CancelButton(language), PendingChangesChoice.Cancel),
            (Localization.DiscardChanges(language), PendingChangesChoice.Discard),
            (Localization.Save(language), PendingChangesChoice.Save)
        })
        {
            var button = new Button { Content = text, MinHeight = 32,
                IsCancel = choice == PendingChangesChoice.Cancel,
                Theme = Application.Current?.FindResource("FlatButton") as ControlTheme };
            button.Click += (_, _) => Close(choice);
            buttons.Children.Add(button);
            if (choice == PendingChangesChoice.Cancel) cancelButton = button;
        }
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16), Spacing = 24,
            Children = { new TextBlock { Text = Localization.PendingChangesPrompt(language),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons }
        };
        Opened += (_, _) => cancelButton?.Focus();
    }
}
