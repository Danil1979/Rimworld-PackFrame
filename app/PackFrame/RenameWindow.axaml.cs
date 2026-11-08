using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PackFrame;

public partial class RenameWindow : Window
{
    private string? _language;
    private string _confirmText = string.Empty;
    private bool _saving;
    private bool _closed;
    internal Func<string, Task<string?>>? TryRenameAsync { get; set; }

    public RenameWindow()
    {
        InitializeComponent();
        Closing += (_, e) => { if (_saving) e.Cancel = true; };
        Closed += (_, _) => _closed = true;
    }

    public RenameWindow(string currentName, string confirmText, string cancelText, string? language = null,
        string? instanceFolder = null) : this()
    {
        _language = language;
        _confirmText = confirmText;
        var label = OperationText.Choose(language, "Instance name", "实例名称", "實例名稱", "インスタンス名");
        this.FindControl<TextBlock>("NameLabel")!.Text = label;
        Title = label;
        this.FindControl<TextBlock>("ScopeText")!.Text = RenameWorkflow.Scope(language);
        this.FindControl<TextBlock>("FolderLabel")!.Text = RenameWorkflow.Folder(language);
        this.FindControl<SelectableTextBlock>("FolderText")!.Text = instanceFolder;
        this.FindControl<StackPanel>("FolderPanel")!.IsVisible = !string.IsNullOrWhiteSpace(instanceFolder);
        var nameBox = this.FindControl<TextBox>("NameBox")!;
        AutomationProperties.SetName(nameBox, label);
        nameBox.Text = currentName;
        this.FindControl<Button>("ConfirmButton")!.Content = confirmText;
        this.FindControl<Button>("CancelButton")!.Content = cancelText;
        this.FindControl<Button>("LogButton")!.Content = Localization.LogButton(language);
        Opened += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
        nameBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await ConfirmAsync();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (!_saving) Close(null);
            }
        };
        nameBox.TextChanged += (_, _) =>
        {
            if (_saving) return;
            this.FindControl<TextBlock>("ValidationText")!.IsVisible = false;
            this.FindControl<Button>("LogButton")!.IsVisible = false;
            this.FindControl<Border>("NameField")!.Classes.Remove("invalid");
        };
    }

    private async void OnConfirmClick(object? sender, RoutedEventArgs e) => await ConfirmAsync();

    private async Task ConfirmAsync()
    {
        if (_saving || _closed) return;
        var box = this.FindControl<TextBox>("NameBox")!;
        var value = box.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            ShowError(OperationText.Choose(_language, "Enter a name before continuing.",
                "请输入名称后继续。", "請輸入名稱後繼續。", "名前を入力してから続行してください。"), invalid: true);
            box.Focus();
            return;
        }

        string? error = null;
        SetSaving(true);
        try
        {
            error = TryRenameAsync is null ? null : await TryRenameAsync(value);
        }
        catch (Exception exception)
        {
            AppLog.Error("Rename instance", exception);
            error = RenameWorkflow.Failure(_language);
        }
        finally { SetSaving(false); }

        if (_closed) return;
        if (!string.IsNullOrWhiteSpace(error))
        {
            ShowError(error, invalid: false);
            this.FindControl<Button>("ConfirmButton")!.Focus();
            return;
        }
        Close(value);
    }

    private void SetSaving(bool saving)
    {
        _saving = saving;
        this.FindControl<TextBox>("NameBox")!.IsEnabled = !saving;
        this.FindControl<Button>("CancelButton")!.IsEnabled = !saving;
        this.FindControl<Button>("LogButton")!.IsEnabled = !saving;
        var confirm = this.FindControl<Button>("ConfirmButton")!;
        confirm.IsEnabled = !saving;
        confirm.Content = saving ? RenameWorkflow.Saving(_language) : _confirmText;
    }

    private void ShowError(string message, bool invalid)
    {
        var error = this.FindControl<TextBlock>("ValidationText")!;
        error.Text = message;
        error.IsVisible = true;
        this.FindControl<Button>("LogButton")!.IsVisible = !invalid;
        var field = this.FindControl<Border>("NameField")!;
        if (invalid && !field.Classes.Contains("invalid")) field.Classes.Add("invalid");
        else if (!invalid) field.Classes.Remove("invalid");
        Dispatcher.UIThread.Post(() => { if (!_closed) error.BringIntoView(); }, DispatcherPriority.Loaded);
    }

    private async void OnLogClick(object? sender, RoutedEventArgs e)
    {
        if (!_saving && !_closed) await new LogWindow(_language).ShowDialog(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (!_saving) Close(null);
    }
}
