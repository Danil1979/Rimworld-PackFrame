using Avalonia.Input;
using PackFrame;

internal static class ModPaneKeyboardChecks
{
    internal static void Run()
    {
        Require(ModPaneKeyboard.Resolve(Key.Down, KeyModifiers.None, true, false) == ModPaneKeyAction.Next,
            "Down moves selection only inside the focused mod list");
        Require(ModPaneKeyboard.Resolve(Key.Up, KeyModifiers.Shift, true, false) == ModPaneKeyAction.ExtendPrevious &&
            ModPaneKeyboard.Resolve(Key.Down, KeyModifiers.Shift, true, false) == ModPaneKeyAction.ExtendNext,
            "Shift arrows retain multi-selection range commands");
        foreach (var key in new[] { Key.Up, Key.Down, Key.Enter, Key.Apps, Key.F10 })
            foreach (var modifiers in new[] { KeyModifiers.None, KeyModifiers.Shift })
                Require(ModPaneKeyboard.Resolve(key, modifiers, false, false) == ModPaneKeyAction.None,
                    $"{modifiers}+{key} on buttons, splitters or other controls does not operate the last mod pane");
        foreach (var modifiers in new[] { KeyModifiers.Control, KeyModifiers.Meta, KeyModifiers.Alt,
                     KeyModifiers.Control | KeyModifiers.Shift })
            Require(ModPaneKeyboard.Resolve(Key.Down, modifiers, true, false) == ModPaneKeyAction.None,
                $"{modifiers}+Down is not captured as plain mod navigation");
        Require(ModPaneKeyboard.Resolve(Key.Down, KeyModifiers.None, false, true) == ModPaneKeyAction.FocusResults &&
            ModPaneKeyboard.Resolve(Key.Enter, KeyModifiers.None, false, true) == ModPaneKeyAction.FocusResults,
            "Down and Enter from search focus results without enabling or disabling a mod");
        Require(ModPaneKeyboard.Resolve(Key.Up, KeyModifiers.None, false, true) == ModPaneKeyAction.None &&
            ModPaneKeyboard.Resolve(Key.Down, KeyModifiers.Shift, false, true) == ModPaneKeyAction.None,
            "search editing and modified arrows are not redirected to list commands");
        Require(ModPaneKeyboard.Resolve(Key.Enter, KeyModifiers.None, true, false) == ModPaneKeyAction.Toggle &&
            ModPaneKeyboard.Resolve(Key.Enter, KeyModifiers.Alt, true, false) == ModPaneKeyAction.None,
            "only plain Enter in a mod list toggles the selected mods");
        Require(ModPaneKeyboard.Resolve(Key.F10, KeyModifiers.Shift, true, false) == ModPaneKeyAction.ContextMenu &&
            ModPaneKeyboard.Resolve(Key.Apps, KeyModifiers.None, true, false) == ModPaneKeyAction.ContextMenu,
            "Shift+F10 and Menu open existing mod actions for the focused list");
        Require(ModPaneKeyboard.Resolve(Key.F10, KeyModifiers.Shift, false, true) == ModPaneKeyAction.None &&
            ModPaneKeyboard.Resolve(Key.Apps, KeyModifiers.None, false, true) == ModPaneKeyAction.None,
            "search retains its own context-menu keys");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
}
