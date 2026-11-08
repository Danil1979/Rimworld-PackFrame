using Avalonia.Input;

namespace PackFrame;

internal enum ModPaneKeyAction { None, FocusResults, Previous, Next, ExtendPrevious, ExtendNext, Toggle, ContextMenu }

internal static class ModPaneKeyboard
{
    internal static ModPaneKeyAction Resolve(Key key, KeyModifiers modifiers, bool listFocused, bool searchFocused)
    {
        if (searchFocused && modifiers == KeyModifiers.None && key is Key.Down or Key.Enter)
            return ModPaneKeyAction.FocusResults;
        if (!listFocused) return ModPaneKeyAction.None;
        if ((key == Key.Apps && modifiers == KeyModifiers.None) ||
            (key == Key.F10 && modifiers == KeyModifiers.Shift)) return ModPaneKeyAction.ContextMenu;
        if (modifiers == KeyModifiers.None)
            return key switch
            {
                Key.Up => ModPaneKeyAction.Previous,
                Key.Down => ModPaneKeyAction.Next,
                Key.Enter => ModPaneKeyAction.Toggle,
                _ => ModPaneKeyAction.None
            };
        if (modifiers == KeyModifiers.Shift)
            return key switch
            {
                Key.Up => ModPaneKeyAction.ExtendPrevious,
                Key.Down => ModPaneKeyAction.ExtendNext,
                _ => ModPaneKeyAction.None
            };
        return ModPaneKeyAction.None;
    }
}
