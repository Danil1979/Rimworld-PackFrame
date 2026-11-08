using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace PackFrame;

internal static class ModColorChoiceCommit
{
    internal static bool SaveLabels(string packRoot, ModColorChoice? choice)
    {
        if (choice is null || choice.Cancelled) return false;
        ModColorStore.SetLabels(packRoot, choice.Labels);
        return true;
    }
}

public partial class ColorPaletteWindow : Window
{
    private static readonly string[] Palette =
    {
        "#E05A5A",
        "#E07A4F",
        "#E0A64F",
        "#D6C85A",
        "#8FCB6A",
        "#4FC18F",
        "#4FC1C1",
        "#5AA9E0",
        "#5A7FE0",
        "#8A6FE0",
        "#C16AD6",
        "#E06AB0",
        "#E06A8A",
        "#B8C4D4",
        "#EAF1F8",
        "#9AA7B8",
        "#7A8A9E",
        "#6E7F91",
        "#A88A6A",
        "#C2A36B",
        "#6FAF8F",
        "#4F8F8A",
        "#6F7FBF",
        "#9F6FBF",
        "#C25A6A",
        "#5A6A7A"
    };

    internal Action<ModColorChoice>? CommitChoice { get; set; }
    private string? _language;

    private readonly Dictionary<string, TextBox> _labelBoxes =
        new(StringComparer.OrdinalIgnoreCase);

    public ColorPaletteWindow()
    {
        InitializeComponent();
    }

    public ColorPaletteWindow(
        string? currentColor,
        IReadOnlyDictionary<string, string> labels,
        string? language
    )
        : this()
    {
        _language = language;
        var title = Localization.ColorPaletteTitle(language);
        Title = $"Rimworld PackFrame - {title}";
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("HintText")!.Text =
            Localization.ColorPaletteHint(language) + "\n" + OperationText.Choose(language,
                "Choose a color or Clear color to save labels. Cancel keeps your current settings.",
                "选择颜色或清除颜色时保存标签。取消将保留当前设置。",
                "選擇顏色或清除顏色時儲存標籤。取消將保留目前設定。",
                "色の選択または色の解除でラベルを保存します。キャンセルすると現在の設定を保持します。");
        this.FindControl<Button>("ClearButton")!.Content =
            Localization.ClearColor(language);
        this.FindControl<Button>("CancelButton")!.Content =
            Localization.CancelButton(language);

        var panel = this.FindControl<WrapPanel>("PalettePanel")!;
        foreach (var hex in Palette)
        {
            var color = Color.Parse(hex);
            var isCurrent = string.Equals(
                hex,
                currentColor,
                StringComparison.OrdinalIgnoreCase
            );
            var item = new StackPanel
            {
                Width = 132,
                Margin = new Thickness(4, 2)
            };
            var button = new Button
            {
                Width = 132,
                Height = 32,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(color),
                Theme = (ControlTheme)this.FindResource("PaletteSwatchButton")!,
                // A fixed-contrast shape identifies the current color, including pale swatches.
                Content = isCurrent ? CreateCurrentColorMarker() : null
            };
            ToolTip.SetTip(button, hex);
            var selectedHex = hex;
            button.Click += (_, _) => Choose(selectedHex);
            item.Children.Add(button);

            var labelBox = new TextBox
            {
                Width = 132,
                Height = 26,
                Margin = new Thickness(0, 4, 0, 0),
                Padding = new Thickness(6, 2),
                Background = ThemeService.SecondaryBrush,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                FontSize = 13,
                PlaceholderText = Localization.ColorLabelPlaceholder(language),
                Text = labels.TryGetValue(hex, out var label) ? label : string.Empty
            };
            var chooseText = OperationText.Choose(language,
                "Choose color ", "选择颜色 ", "選擇顏色 ", "色を選択 ") + hex;
            var currentText = isCurrent ? OperationText.Choose(language,
                " (current color)", "（当前颜色）", "（目前顏色）", "（現在の色）") : string.Empty;
            void UpdateAccessibleName()
            {
                var labelText = labelBox.Text?.Trim();
                var name = chooseText + currentText + (string.IsNullOrEmpty(labelText) ? string.Empty : ": " + labelText);
                AutomationProperties.SetName(button, name);
                ToolTip.SetTip(button, name);
            }
            UpdateAccessibleName();
            labelBox.TextChanged += (_, _) => UpdateAccessibleName();
            AutomationProperties.SetName(labelBox, OperationText.Choose(language,
                "Label for color ", "颜色标签 ", "顏色標籤 ", "色のラベル ") + hex);
            AutomationProperties.SetLabeledBy(labelBox, button);
            AutomationProperties.SetHelpText(labelBox, Localization.ColorPaletteHint(language));
            item.Children.Add(labelBox);
            _labelBoxes[hex] = labelBox;
            panel.Children.Add(item);
        }

        KeyDown += OnWindowKeyDown;
        Opened += (_, _) => this.FindControl<Button>("CancelButton")!.Focus();
    }

    private static Border CreateCurrentColorMarker() => new()
    {
        Width = 20,
        Height = 20,
        CornerRadius = new CornerRadius(10),
        Background = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#101820")),
        BorderThickness = new Thickness(1),
        IsHitTestVisible = false,
        Child = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M 0,4 L 3,7 L 9,0"),
            Stroke = new SolidColorBrush(Color.Parse("#101820")),
            StrokeThickness = 2,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        }
    };

    private Dictionary<string, string> ReadLabels()
    {
        var labels = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase
        );
        foreach (var pair in _labelBoxes)
        {
            labels[pair.Key] = pair.Value.Text?.Trim() ?? string.Empty;
        }

        return labels;
    }

    private void Choose(string hex)
    {
        TryChoose(new ModColorChoice(false, false, hex, ReadLabels()));
    }

    private void TryChoose(ModColorChoice choice)
    {
        try
        {
            CommitChoice?.Invoke(choice);
            Close(choice);
        }
        catch (Exception exception)
        {
            AppLog.Error("Save color choices", exception);
            this.FindControl<SelectableTextBlock>("ErrorText")!.Text = OperationText.Choose(_language,
                "Could not save colors. Your choices are kept here. Fix the problem, then choose again.",
                "无法保存颜色。此处保留了您的选择。修复问题后请重试。",
                "無法儲存顏色。此處保留了您的選擇。修復問題後請重試。",
                "色を保存できませんでした。入力内容は保持されています。問題を修正して再度選択してください。") + "\n" + exception.Message;
            this.FindControl<Border>("ErrorBorder")!.IsVisible = true;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        TryChoose(new ModColorChoice(false, true, null, ReadLabels()));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
