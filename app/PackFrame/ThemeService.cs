using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace PackFrame;

internal static class ThemeService
{
    public const string DefaultBackground = "#171C22";

    public static readonly (string Key, string Hex, string? Light)[] BackgroundPresets =
    {
        ("default", "#171C22", null),
        ("dark", "#182938", null),
        ("black", "#15181D", null),
        ("rimworld", "#15191D", "#3C4A50"),
        ("purple", "#251F33", null),
        ("green", "#1D2C27", null),
        ("brown", "#2B2421", null)
    };

    public static SolidColorBrush BackgroundBrush { get; private set; } =
        new(Color.Parse(DefaultBackground));

    public static SolidColorBrush SecondaryBrush { get; } =
        new(Color.Parse("#455364"));

    public static SolidColorBrush PrimaryTextBrush { get; } =
        new(Color.Parse("#EAF1F8"));

    public static SolidColorBrush SecondaryTextBrush { get; } =
        new(Color.Parse("#C3D4E4"));

    public static SolidColorBrush MutedTextBrush { get; } =
        new(Color.Parse("#9AA7B8"));

    public static SolidColorBrush HoverBrush { get; } =
        new(Color.Parse("#4D6378"));

    public static SolidColorBrush SelectedBrush { get; } =
        new(Color.Parse("#5C6E82"));

    public static void Apply(string? hex, string? lightHex = null)
    {
        var value = Normalize(hex);
        var background = Color.Parse(value);
        BackgroundBrush = new SolidColorBrush(background);
        if (string.IsNullOrWhiteSpace(lightHex))
        {
            lightHex = BackgroundPresets
                .FirstOrDefault(preset =>
                    preset.Hex.Equals(value, StringComparison.OrdinalIgnoreCase))
                .Light;
        }
        SecondaryBrush.Color = TryParseColor(lightHex) ??
            Mix(background, Colors.White, 0.16);
        var isLight = Luminance(background) > 128;
        var textBase = isLight ? Colors.Black : Colors.White;
        PrimaryTextBrush.Color = Mix(background, textBase, 0.90);
        SecondaryTextBrush.Color = Mix(background, textBase, 0.75);
        MutedTextBrush.Color = Mix(background, textBase, 0.55);
        HoverBrush.Color = Mix(background, textBase, 0.20);
        SelectedBrush.Color = TryParseColor(lightHex) ??
            Mix(background, textBase, 0.27);
        if (Application.Current is { } application)
        {
            // Windows intentionally retain Fluent's dark control surfaces. Only adapt
            // template parts that sit on our custom light surfaces; remove overrides
            // on return to a dark palette so Fluent's original colors remain intact.
            foreach (var (key, brush) in new (string, SolidColorBrush)[]
            {
                ("TextControlPlaceholderForeground", SecondaryTextBrush),
                ("TextControlPlaceholderForegroundPointerOver", SecondaryTextBrush),
                ("TextControlPlaceholderForegroundFocused", SecondaryTextBrush),
                ("TextControlPlaceholderForegroundDisabled", MutedTextBrush),
                ("ComboBoxDropDownGlyphForeground", PrimaryTextBrush),
                ("ComboBoxDropDownGlyphForegroundDisabled", MutedTextBrush),
                ("ComboBoxDropDownGlyphForegroundFocused", PrimaryTextBrush),
                ("ComboBoxDropDownGlyphForegroundFocusedPressed", PrimaryTextBrush),
                ("ComboBoxForeground", PrimaryTextBrush),
                ("ComboBoxForegroundDisabled", MutedTextBrush),
                ("ComboBoxForegroundFocused", PrimaryTextBrush),
                ("ComboBoxForegroundFocusedPressed", PrimaryTextBrush),
                ("ComboBoxBackground", SecondaryBrush),
                ("ComboBoxBackgroundDisabled", SecondaryBrush),
                ("ComboBoxBackgroundUnfocused", SecondaryBrush),
                ("ComboBoxBackgroundPointerOver", HoverBrush),
                ("ComboBoxBackgroundPressed", SelectedBrush)
            })
            {
                if (isLight)
                    application.Resources[key] = brush;
                else
                    application.Resources.Remove(key);
            }
            // Fluent also halves placeholder opacity. Our adaptive text is already
            // subdued; halving it again fails contrast against light search fields.
            if (isLight)
                application.Resources["TextControlPlaceholderOpacity"] = 1.0;
            else
                application.Resources.Remove("TextControlPlaceholderOpacity");
            application.Resources["AppBackgroundBrush"] = BackgroundBrush;
            application.Resources["PaneSurfaceBrush"] = new SolidColorBrush(Mix(background, textBase, 0.035));
            application.Resources["FieldSurfaceBrush"] = new SolidColorBrush(Mix(background, textBase, 0.075));
            UpdateResource(application, "SecondaryBrush", SecondaryBrush);
            UpdateResource(application, "PrimaryTextBrush", PrimaryTextBrush);
            UpdateResource(application, "SecondaryTextBrush", SecondaryTextBrush);
            UpdateResource(application, "MutedTextBrush", MutedTextBrush);
            UpdateResource(application, "ItemHoverBrush", HoverBrush);
            UpdateResource(application, "ItemSelectedBrush", SelectedBrush);
        }
    }

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            return Color.Parse(hex.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static double Luminance(Color color) =>
        (color.R + color.G + color.B) / 3.0;

    private static void UpdateResource(
        Application application,
        string key,
        SolidColorBrush brush
    )
    {
        if (application.Resources[key] is SolidColorBrush existing)
        {
            existing.Color = brush.Color;
        }
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        return Color.FromArgb(
            255,
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount)
        );
    }

    private static string Normalize(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return DefaultBackground;
        }

        try
        {
            Color.Parse(hex.Trim());
            return hex.Trim();
        }
        catch
        {
            return DefaultBackground;
        }
    }
}
