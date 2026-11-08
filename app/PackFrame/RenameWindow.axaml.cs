using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class RenameWindow : Window
{
    private string? _language;
    public RenameWindow()
    {
        InitializeComponent();
    }

    public RenameWindow(string currentName, string confirmText, string cancelText, string? language = null)
        : this()
    {
        _language = language;
        var label = OperationText.Choose(language, "Instance name", "实例名称", "實例名稱", "インスタンス名");
        this.FindControl<TextBlock>("NameLabel")!.Text = label;
        Title = label;
        AutomationProperties.SetName(this.FindControl<TextBox>("NameBox")!, label);
        this.FindControl<TextBox>("NameBox")!.Text = currentName;
        this.FindControl<Button>("ConfirmButton")!.Content = confirmText;
        this.FindControl<Button>("CancelButton")!.Content = cancelText;
        Opened += (_, _) =>
        {
            var nameBox = this.FindControl<TextBox>("NameBox")!;
            nameBox.Focus();
            nameBox.SelectAll();
            nameBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Confirm();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    Close(null);
                    e.Handled = true;
                }
            };
        };
        this.FindControl<TextBox>("NameBox")!.TextChanged += (_, _) =>
            this.FindControl<TextBlock>("ValidationText")!.IsVisible = false;
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Confirm();
    }

    private void Confirm()
    {
        var box = this.FindControl<TextBox>("NameBox")!;
        var value = box.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            var error = this.FindControl<TextBlock>("ValidationText")!;
            error.Text = OperationText.Choose(_language, "Enter a name before continuing.",
                "请输入名称后继续。", "請輸入名稱後繼續。", "名前を入力してから続行してください。");
            error.IsVisible = true;
            box.Focus();
            return;
        }
        Close(value);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
