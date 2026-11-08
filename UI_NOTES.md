# Pane UI follow-up

## Implemented refinements

- Order numbers use a compact, right-aligned gutter sized for the full load order, with stable width during filtering. Status badges sit at the trailing edge, keeping mod names near their numbers.
- A subtle tint in the target pane's header complements its outline and follows the configured theme colors.

## Latest user feedback

- The load-order number gutter feels oversized and wastes space.
- A first-time user would not understand why two enabled lists are shown. The current layout relies on prior knowledge of PackFrame's workflow; `Same list` only describes their relationship.

## Discovery workflow

- New installations start with one enabled pane; saved single/split layout preferences remain respected.
- `在右侧打开 / Open on right` is attached to the left list's header. It opens that instance in the right pane and focuses its independent search, while retaining the left selection.
- The right header has a separate close control. Closing returns its enable/launch target to the visible pane. Reopening the same instance retains its search; opening a different instance clears the old query.
- Instance names identify the content of each pane. Both remain editable, and choosing a different instance supports copying mods between them. Functional control labels and tooltips provide context without an instructional caption.

References: VS Code's Split Editor / Open to the Side, Notepad++'s Clone to Other View, and Dolphin's Split view. The opening action establishes why another view exists.
