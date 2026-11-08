using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using PackFrame;

internal static class ThemeContrastChecks
{
    public static void Run()
    {
        AppBuilder.Configure<Application>().UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(() => { }, "ResourceChecks")
            .UseSkia().UseHarfBuzz()
            .SetupWithoutStarting();
        var app = Application.Current!;
        app.RequestedThemeVariant = ThemeVariant.Dark;
        app.Styles.Add(new FluentTheme());
        // No windows or icon loader are needed to exercise the actual Fluent resources.
        foreach (var key in new[] { "SecondaryBrush", "PrimaryTextBrush", "SecondaryTextBrush",
                     "MutedTextBrush", "ItemHoverBrush", "ItemSelectedBrush" })
            app.Resources[key] = new SolidColorBrush();
        var defaultPlaceholderOpacity = (double)app.FindResource("TextControlPlaceholderOpacity")!;
        Check(defaultPlaceholderOpacity == 0.5, "Fluent placeholder template defaults to half opacity");
        foreach (var (background, expected) in new[]
        {
            (ThemeService.DefaultBackground, ThemeVariant.Dark),
            ("#F0F2F4", ThemeVariant.Light),
            ("#FFFFFF", ThemeVariant.Light),
            ("#182938", ThemeVariant.Dark),
            ("not a color", ThemeVariant.Dark)
        })
        {
            ThemeService.Apply(background);
            Check(app.RequestedThemeVariant == ThemeVariant.Dark,
                $"custom background preserves pinned Fluent dark surfaces {background}");
            foreach (var key in new[] { "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver",
                         "TextControlPlaceholderForegroundFocused", "TextControlPlaceholderForegroundDisabled",
                         "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundDisabled" })
            {
                Check(app.TryGetResource(key, ThemeVariant.Dark, out var value) && value is ISolidColorBrush,
                    $"Fluent resource resolves {key} for {background}");
                if (expected == ThemeVariant.Light)
                    Check(Contrast(((ISolidColorBrush)value!).Color, ThemeService.BackgroundBrush.Color) > 3,
                        $"template foreground contrasts with light custom background: {key}");
                else
                    Check(!app.Resources.ContainsKey(key), $"dark template restores Fluent default: {key}");
            }
            var placeholderOpacity = (double)app.FindResource("TextControlPlaceholderOpacity")!;
            if (expected == ThemeVariant.Light)
            {
                var field = ((ISolidColorBrush)app.Resources["FieldSurfaceBrush"]!).Color;
                // Search TextBoxes add a 1/255 black fill over the field surface.
                var searchSurface = Color.FromRgb((byte)Math.Round(field.R * 254.0 / 255),
                    (byte)Math.Round(field.G * 254.0 / 255), (byte)Math.Round(field.B * 254.0 / 255));
                foreach (var key in new[] { "TextControlPlaceholderForeground",
                             "TextControlPlaceholderForegroundPointerOver", "TextControlPlaceholderForegroundFocused" })
                {
                    var foreground = ((ISolidColorBrush)app.FindResource(key)!).Color;
                    Check(Contrast(foreground, searchSurface, placeholderOpacity) >= 4.5,
                        $"effective placeholder contrast includes template opacity: {key} on {background}");
                }
            }
            else
                Check(placeholderOpacity == defaultPlaceholderOpacity &&
                      !app.Resources.ContainsKey("TextControlPlaceholderOpacity"),
                    $"dark placeholder restores original opacity after theme changes: {background}");
            foreach (var (glyphKey, surfaceKey) in new[]
            {
                ("ComboBoxDropDownGlyphForeground", "ComboBoxBackground"),
                ("ComboBoxDropDownGlyphForeground", "ComboBoxBackgroundPointerOver"),
                ("ComboBoxDropDownGlyphForegroundFocused", "ComboBoxBackgroundUnfocused"),
                ("ComboBoxDropDownGlyphForegroundFocusedPressed", "ComboBoxBackgroundPressed")
            })
            {
                Check(app.TryGetResource(glyphKey, ThemeVariant.Dark, out var glyph) && glyph is ISolidColorBrush,
                    $"dropdown glyph resource resolves {glyphKey}");
                Check(app.TryGetResource(surfaceKey, ThemeVariant.Dark, out var surface) && surface is ISolidColorBrush,
                    $"dropdown surface resource resolves {surfaceKey}");
                if (expected == ThemeVariant.Light)
                    Check(Contrast(((ISolidColorBrush)glyph!).Color, ((ISolidColorBrush)surface!).Color) > 4.5,
                        $"light dropdown stays legible in {surfaceKey} state");
                else
                    Check(!app.Resources.ContainsKey(glyphKey) && !app.Resources.ContainsKey(surfaceKey),
                        $"dark dropdown restores original {surfaceKey} state");
            }
            Check(Contrast(ThemeService.PrimaryTextBrush.Color, ThemeService.SecondaryBrush.Color) > 4.5,
                $"idle brush label contrasts with button surface {background}");
        }
        ThemeService.Apply(ThemeService.DefaultBackground);
    }

    private static double Contrast(Color foreground, Color background, double opacity = 1)
    {
        var alpha = foreground.A / 255.0 * opacity;
        var opaque = Color.FromRgb(
            (byte)Math.Round(foreground.R * alpha + background.R * (1 - alpha)),
            (byte)Math.Round(foreground.G * alpha + background.G * (1 - alpha)),
            (byte)Math.Round(foreground.B * alpha + background.B * (1 - alpha)));
        var first = Luminance(opaque);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte component)
        {
            var value = component / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
