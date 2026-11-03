using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PackFrame;

public enum CompletionChoice
{
    Close = 0,
    MissingMods = 1,
    OpenFolder = 2
}

public partial class CompletionWindow : Window
{
    public CompletionWindow()
    {
        InitializeComponent();
    }

    public CompletionWindow(
        string title,
        string message,
        string? missingText,
        string openFolderText,
        string closeText
    )
        : this()
    {
        Title = "Rimworld PackFrame - " + title;
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        var missing = this.FindControl<Button>("MissingButton")!;
        missing.Content = missingText;
        missing.IsVisible = !string.IsNullOrWhiteSpace(missingText);
        this.FindControl<Button>("OpenFolderButton")!.Content = openFolderText;
        this.FindControl<Button>("CloseButton")!.Content = closeText;
        KeyDown += OnWindowKeyDown;
    }

    private void OnMissingClick(object? sender, RoutedEventArgs e)
    {
        Close(CompletionChoice.MissingMods);
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        Close(CompletionChoice.OpenFolder);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(CompletionChoice.Close);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(CompletionChoice.Close);
            e.Handled = true;
        }
    }
}
