using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class LanguageWindow : Window
{
    public event EventHandler<string>? LanguageSelected;

    public LanguageWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }

    private void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string language })
        {
            LanguageSelected?.Invoke(this, language);
        }
    }
}
