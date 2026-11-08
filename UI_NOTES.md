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

## Readability and accessibility pass

- Details use a proportional, draggable column (220–380 px) instead of a fixed
  420 px. Missing previews collapse, and long mod names wrap. Empty details show
  localized setup and edit/save guidance in a scrollable panel.
- Footer filters are wrapping buttons labelled Order, Version, Duplicate and Local
  (translated in all supported languages); per-mod S/V/R/L badges stay compact.
  Missing/download status occupies its own row. Tab, Space and Enter can operate
  filters, and accessible names include the current count and filter state.
- Flat buttons now distinguish hover, pressed, keyboard focus and disabled states.
  Mod rows expose their name and status to assistive technology, not the entire
  internal model. Icon-only instance and search controls have meaningful names.
- Catalog failures show a wrapped notice and expandable error details. Bundled
  and saved catalogs are distinguished, and detail failures preserve the selected
  pack's name/summary with retry guidance.

Verified on a Linux desktop with synthetic audit data: single/split panes,
1100×700 minimum layout, divider dragging, split close/open, keyboard footer
filter cycling, first-run/invalid-path guidance, and accessible names. Release
build and full regression checks passed. Offline cache behavior has automated
coverage; the updated offline notice was not manually GUI-tested. Real game
launches and native Windows/macOS GUI behavior remain platform checks.
