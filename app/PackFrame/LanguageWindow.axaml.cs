using System;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace PackFrame;

public partial class LanguageWindow : Window
{
    public event EventHandler<string>? LanguageSelected;

    public LanguageWindow() : this(null) { }

    public LanguageWindow(string? currentLanguage)
    {
        InitializeComponent();
        Title = LanguageChoice.Title(currentLanguage);
        this.FindControl<TextBlock>("HeadingText")!.Text = Localization.IsSupported(currentLanguage)
            ? Title : "Language · 语言 · 言語";
        this.FindControl<TextBlock>("HintText")!.Text = OperationText.Choose(currentLanguage,
            "Choose your display language. Your mod lists stay as they are.",
            "选择界面语言。模组列表将保持不变。", "選擇介面語言。模組清單將保持不變。",
            "表示言語を選択してください。MOD リストはそのまま保持されます。");
        foreach (var choice in LanguageChoice.Options(currentLanguage))
        {
            var button = this.FindControl<Button>(choice.ControlName)!;
            button.Content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center,
                Children = {
                    new TextBlock { Text = choice.NativeName, FontSize = 16, FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = choice.CurrentLabel, IsVisible = choice.IsCurrent, FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center } } };
            AutomationProperties.SetName(button, choice.AccessibleName);
        }
        var cancel = this.FindControl<Button>("CancelButton")!;
        cancel.Content = Localization.IsSupported(currentLanguage) ? Localization.CancelButton(currentLanguage)
            : "Close / 关闭 / 閉じる";
        Opened += (_, _) => cancel.Focus();
    }

    private void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string language } && Localization.IsSupported(language))
        {
            LanguageSelected?.Invoke(this, language);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
