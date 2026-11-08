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
        var heading = Localization.ImportInstance(language);
        Title = heading;
        this.FindControl<TextBlock>("HeadingText")!.Text = heading;
        this.FindControl<TextBlock>("HintText")!.Text = InstanceImportReview.Hint(language);
        var unavailable = this.FindControl<TextBlock>("UnavailableText")!;
        unavailable.IsVisible = !overwriteEnabled;
        unavailable.Text = OperationText.Choose(language,
            "Replacement is unavailable for the current selection. Create a new instance instead.",
            "当前选择无法替换。请新建实例。", "目前選擇無法取代。請建立新實例。", "現在の選択は置き換えられません。新しいインスタンスを作成してください。");
        this.FindControl<Button>("CancelButton")!.Content = Localization.CancelButton(language);
        Opened += (_, _) => this.FindControl<Button>("CancelButton")!.Focus();
    }

    internal ImportModeWindow(InstanceImportReview review, bool overwriteEnabled, string? language = null)
        : this(InstanceImportReview.ReviewLabel(language), InstanceImportReview.NewLabel(language), overwriteEnabled, language)
    {
        this.FindControl<StackPanel>("ReviewPanel")!.IsVisible = true;
        this.FindControl<TextBlock>("SourceLabel")!.Text = InstanceImportReview.SourceLabel(language);
        this.FindControl<SelectableTextBlock>("SourceText")!.Text = review.SourcePath;
        this.FindControl<TextBlock>("TargetLabel")!.Text = InstanceImportReview.TargetLabel(language);
        this.FindControl<TextBlock>("TargetNameText")!.Text = review.TargetName;
        this.FindControl<SelectableTextBlock>("TargetPathText")!.Text = review.DestinationPath;
        this.FindControl<TextBlock>("ScopeLabel")!.Text = InstanceImportReview.ScopeLabel(language);
        this.FindControl<TextBlock>("ScopeText")!.Text = review.Scope(language);
        this.FindControl<TextBlock>("PendingText")!.Text = review.PendingWarning(language);
        this.FindControl<TextBlock>("PendingText")!.IsVisible = review.HasPendingChanges;
        AutomationProperties.SetHelpText(this.FindControl<Button>("OverwriteButton")!,
            review.TargetName + "\n" + review.DestinationPath + "\n" + review.Scope(language));
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
