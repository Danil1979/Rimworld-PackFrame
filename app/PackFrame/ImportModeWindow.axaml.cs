using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Interactivity;

namespace PackFrame;

public enum ImportMode
{
    Overwrite,
    New
}

public partial class ImportModeWindow : Window
{
    public ImportModeWindow()
    {
        InitializeComponent();
    }

    public ImportModeWindow(string overwriteText, string newText, bool overwriteEnabled, string? language = null)
        : this()
    {
        var overwriteButton = this.FindControl<Button>("OverwriteButton")!;
        var newButton = this.FindControl<Button>("NewButton")!;
        this.FindControl<TextBlock>("OverwriteText")!.Text = overwriteText;
        overwriteButton.IsEnabled = overwriteEnabled;
        this.FindControl<TextBlock>("NewText")!.Text = newText;
        AutomationProperties.SetName(overwriteButton, overwriteText);
        AutomationProperties.SetName(newButton, newText);
        var heading = OperationText.Choose(language, "Import mod list", "导入模组列表", "匯入模組清單", "MOD リストのインポート");
        Title = heading;
        this.FindControl<TextBlock>("HeadingText")!.Text = heading;
        this.FindControl<TextBlock>("HintText")!.Text = OperationText.Choose(language,
            "Create a separate instance, or replace the selected instance's mod list. Replacement will ask for confirmation.",
            "新建独立实例，或替换所选实例的模组列表。替换前会要求确认。",
            "建立獨立實例，或取代所選實例的模組清單。取代前會要求確認。",
            "別のインスタンスを作成するか、選択中の MOD リストを置き換えます。置き換え前に確認します。");
        var unavailable = this.FindControl<TextBlock>("UnavailableText")!;
        unavailable.IsVisible = !overwriteEnabled;
        unavailable.Text = OperationText.Choose(language,
            "Replacement is unavailable for the current selection. Create a new instance instead.",
            "当前选择无法替换。请新建实例。", "目前選擇無法取代。請建立新實例。", "現在の選択は置き換えられません。新しいインスタンスを作成してください。");
        this.FindControl<Button>("CancelButton")!.Content = Localization.CancelButton(language);
        Opened += (_, _) => this.FindControl<Button>("CancelButton")!.Focus();
    }

    private void OnOverwriteClick(object? sender, RoutedEventArgs e)
    {
        Close(ImportMode.Overwrite);
    }

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        Close(ImportMode.New);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
