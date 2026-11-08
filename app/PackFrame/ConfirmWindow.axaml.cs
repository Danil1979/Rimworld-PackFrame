using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
        Opened += (_, _) => this.FindControl<Button>("CancelButton")!.Focus();
    }

    public ConfirmWindow(string message, string confirmText, string cancelText)
        : this()
    {
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        this.FindControl<Button>("ConfirmButton")!.Content = confirmText;
        this.FindControl<Button>("CancelButton")!.Content = cancelText;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
