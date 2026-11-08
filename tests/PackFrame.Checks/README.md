# PackFrame regression checks

Run from the repository root (close the development app first):

```powershell
dotnet run --project tests/PackFrame.Checks -c Release
```

The checks use temporary instances and remove them afterward. They cover metadata
preservation, saving missing package IDs, interrupted atomic writes, successful
config commits, and rollback of overwritten/new files on commit failure.
They also exercise actual mod links (Windows junctions / Unix symbolic links),
cross-instance recovery, broken-link cleanup, ownership checks, preparation
rollback, color-cache invalidation, and corrupt-configuration diagnostics.
The tests call the application's internal implementations through
`InternalsVisibleTo`, rather than compiling separate copies of production files.
List tests cover contiguous/noncontiguous moves, insertion boundaries and previews,
case-insensitive deduplication, true load-order positions through filtering and
missing entries, pane selection restoration, two-instance undo/redo, and missing
undo targets.
Launch tests inspect arguments without launching RimWorld; installer tests use
temporary mod/config files without downloading anything.
Catalog checks use an injected HTTP handler to cover offline bundled startup,
ETag/304 and Last-Modified checks, changed hashes, persistent and corrupt caches,
source isolation, mirror-relative URLs, cancellation, and unwritable cache paths.
They also verify startup and window refreshes share one request while each caller
can cancel independently, and that saved source choices are preserved.
Pack-detail checks cover revisits without another fetch, restart persistence,
mirror aliases, mutable-model isolation, changed hashes, and failed/corrupt reads.
Failure checks cover damaged-profile isolation, stale pending edits, installation
rollback and staging-path rejection, recovery across damaged manifests, mutable
color snapshots and failed color writes. Linux/macOS CI additionally constructs
a tar.gz and checks executable permissions through the actual SteamCMD extractor.
Workshop checks cover bounded Steam KeyValues parsing, passive manifest/log observations,
unavailable records, deferred/bounded completion rescans, saved item/package mappings,
restart recovery, and preserving pending edits and unrelated instance metadata.
These checks use synthetic state and temporary files; they do not contact Steam.

Workshop checks cover installed-manifest changes completed between polls,
same-second log/ACF timestamps, and secondary-library discovery using Linux
and macOS client roots. Selection checks repeatedly replace all three panes'
models while retaining selected package IDs, range anchors and displayed details.

Manual platform checks:

- On Windows x64 with Steam running, install a pack whose local download finishes
  before its Workshop items. Queued/downloading items must not count as Missing;
  the status dialog updates while open. Verify Steam keeps downloading while
  PackFrame checks status and does not mark RimWorld as running. Preallocated file
  sizes must not appear as download percentages. Verify the lists refresh after
  each item finishes without pressing Refresh.
  Keep unsaved order edits, selections, searches and scroll positions in both panes
  during completion; they must remain intact. Reopen PackFrame during a download
  and confirm its saved Workshop targets are still monitored.
- Leave monitoring idle, then install a pack with a new Workshop item. Finishing
  the catalogue load must start a fresh Steam query without waiting for the idle
  30-second interval; subsequent pending checks return to four-second polling.
- Pause Steam downloads and resume them; incomplete items remain pending. Make
  Steam's manifest/log records unavailable: uncertain Workshop items show Status unavailable,
  while known missing local/GitHub mods remain Missing. Restart Steam and verify
  monitoring recovers. SteamCMD instances must keep their existing download flow.
- A completed Workshop item with unreadable metadata gets a bounded rescan retry;
  persistent invalid metadata becomes Missing rather than refreshing indefinitely.

- App update checks must only offer published release information. Opening the
  offer must not download, close or restart PackFrame. Download, Later and Install
  and restart are separate choices; closing/cancelling during download keeps the
  running installation intact. A deferred verified download should be reused.
- Cancelling unsaved changes before update installation keeps PackFrame open.
  Only an explicit install followed by the main window's actual close may commit
  the helper. Verify update success/failure receipts using an isolated installation,
  and preserve its settings, instances, saves and mods. Never test replacement
  against a player's running installation.
- The unsaved-changes prompt should fit its wrapped message and buttons without
  the fixed blank area below. Escape cancels; Tab reaches each action.

- Open Modpacks with no saved catalog and a slow/offline connection: the three
  bundled entries should appear immediately and remain selectable during the
  background check. Verify the source choice persists after reopening.
- Change packs or repository sources while details are loading: stale responses
  must not overwrite the new selection. Close during a check, reopen, and test a
  custom catalog without a cache; default bundled entries must never appear for it.
- Let pack A finish loading, select B, and return to A: saved details should appear
  immediately without another request or loading message. Reopen and check that
  saved details remain visible during background validation; Refresh must force
  a new read. A first visit still needs the pack's details from the server.
- Start an install, config apply, or pack binding while a background catalog
  update is pending: it must retain the selected pack until that operation ends.
- With cached pack details, install/apply must read a fresh manifest. If its
  Workshop collection changed, an auto-filled link must follow the fresh pack;
  explicitly edited links remain overrides. Refresh-and-resume must retain the
  original installation target while a newer catalog waits in the background.
- Start with settings that have no saved layout: only one enabled pane appears.
  Select several mods, click its header's "Open on right" action, and confirm the
  same instance opens on the right with search focused and the left selection intact.
- Select the same instance in both enabled panes: both show "Same list", with
  independent search and scroll positions. Select different instances (including
  instances with identical display names): the badges disappear. Change language
  and confirm the badges and launch target are translated.
- Drag within a list or between views of the same instance: the destination is
  outlined, the ghost names the mod before/after the insertion point, and the
  insertion line follows the drop position. A drop that changes nothing says
  "Already here". Drag between different instances: it says "Add to <name>",
  "Reorder in <name>", or "Add and reorder in <name>" depending on existing mods,
  and leaves the source intact. Drag to the inactive pane: it says "Disable in <name>".
  Leaving a valid list, releasing the pointer, or losing pointer capture must
  clear the destination outline and action label.
- Click either enabled pane and change its instance: its header is highlighted
  and the launch button names that target. Check long names at the minimum window width; the complete launch name
  remains available in its tooltip and accessible name.
- Drag a contiguous selection within itself, move a noncontiguous selection to
  the beginning/end, drag between two profiles, and copy into a profile that
  already contains an item. Check the selection after each operation.
- Undo and redo a cross-profile move and a color change. Check Ctrl+Z, Ctrl+Y,
  Ctrl+Shift+Z and the toolbar buttons; new edits must clear redo history.
- Keep testing Shift/Ctrl selection, double-click enable/disable, and drag
  insertion with a search filter. These event/coordinate paths remain in the UI.

- Search quickly in each column: only that column should rebuild after 180ms;
  other columns retain selection and scroll position without rebuilding. Visible
  selected rows in the searched column remain selected; hidden selections are
  cleared safely. Details and keyboard actions
  must target the searched column; zero results must clear details. Switch the target instance and search
  again to check the inactive list uses the new target.
- Select mods rapidly: the preview must match the latest selection, including
  switching to a mod without an image. Close while an image is loading.

- Select several mods in the left enabled pane, search in the right pane, then
  drag the original selection to a distant destination. Repeat with different
  instances. Extend and shrink a range with repeated Shift+Down/Up, then visit the
  other pane and return. Check Shift/Ctrl anchors survive and Delete,
  double-click, color actions and context menus affect only their current pane.
- Filter an enabled list and verify each number stays its full load-order position,
  including gaps for missing entries. Reorder and undo: both views must renumber
  consistently without resetting their independent positions. Gutter width must
  remain stable during filtering, including three/four-digit orders. Status badges
  stay at the right edge; hover them to check the same details and actions.
- All three pane footers show L: <local count>, including zero. Like
  S/V/R, the count follows search/source filters but precedes footer filters.
  Switch each instance, change search/source filters, hide errors, and clear the
  catalogue: L must refresh without changing enabled mods or their order.
  Click L in each pane: strikethrough excludes local mods, underline shows only
  local mods, and the third click clears the filter. The three panes filter
  independently; combining L with S/V/R uses all selected conditions. L keeps
  its pre-filter count even when the visible list becomes empty.
- Hover an error badge or bottom problem summary: it opens after 300ms, not on
  entry. Leaving before 300ms, switching badges, clicking, scrolling, or closing
  the pane/window must cancel the pending opening. Leaving an open popup closes
  it after 100ms; moving into its content keeps copy, text selection, and Apply
  usable. Move between text, column gaps, blank space, and the outer padding:
  all of the visible popup must keep it open, including version/duplicate summaries.
  Click its text or padding, then drag to select text: the popup must stay open
  and the selected span must visibly highlight, including after mouse release.
  A nonempty SelectedText value alone does not verify the selection is visible.
  Popup interaction must not change the summary filter or list selection. Check
  Ctrl+C and the badge copy menu; Apply must still work, and clicking the mod list
  must still close it.
  A copy menu opened from the popup must stay usable beyond the 100ms close delay.
  After clicking or selecting text, move below the whole popup: retained keyboard
  focus must not stop its 100ms dismissal. Drag outside while holding the mouse:
  keep the popup open until release, then resume dismissal. Close a copy menu with
  the pointer outside the popup: dismissal must resume without another mouse move.
  Color-label, launch-target, and damaged-instance tooltips are unchanged.
- Close the second enabled pane using its header control while it is the
  enable/launch target: the target and header highlight must return to the visible
  pane. Open the same instance on the right again: its search and scroll position
  remain intact. Open a different left instance on the right: the previous right
  search is cleared. Reopen the app and check the saved layout preference.
  Hidden panes must not receive keyboard actions or drag targets.
- Errors are recorded in `app/PackFrame/bin/Release/net10.0/packframe.log` in the
  local launcher layout, with one rotated `.1` file after 2 MB.

- With unsaved list edits, refresh and close must offer Save / Discard / Cancel.
  Cancel must leave the edits in place; failed saving must not close the window.
- Interrupt a configuration download: existing configuration files must remain
  unchanged. Malformed XML must be rejected before applying any file.
- On macOS, launch a profile containing local mods: its links must remain until
  the game exits (`open -W -n` waits for the new app instance).
- On Linux/macOS, test first-run SteamCMD installation without a preinstalled
  copy. The platform tar.gz must be extracted, including executable permissions.
  SteamCMD's native runtime prerequisites still apply; Apple Silicon may require
  Rosetta for Valve's Intel binaries.

Readability/accessibility follow-up:

- Footer labels now spell out Order, Version, Duplicate and Local. The existing
  filter checks above apply to those buttons; per-mod S/V/R/L badges are unchanged.
- At 1100×700 in split view, check every footer count and missing/download status
  stays separate. Drag the details divider and use its arrow-key resize behavior.
- Tab to each footer filter and cycle with Space or Enter. Counts stay pre-filter,
  focus stays on the button, and the accessible name describes the current mode.
- Check disabled Undo/Redo and keyboard focus are visibly distinct from enabled
  buttons. Inspect mod row accessible names for the name/status, not a model dump.
- No selected mod shows setup/edit/save instructions; missing previews reserve no
  blank image area. Long mod names wrap and the instruction panel scrolls.

Dialog and operation-state checks also cover palette cancellation, retained wizard
input, destination-specific index retries, anonymous manifest verification,
transactional OSS settings, malformed archive rejection, recursive config rollback,
install cancellation tails, and distinct diagnostic-log read failures. See
[UI review coverage](../../docs/UI_REVIEW.md) for native checks and remaining gaps.

Local-mod copy checks exercise the main-window copy/stage orchestration without a GUI:
missing sources and mid-copy failures preserve pending active IDs, asynchronous copies
finish before activation, successful copies publish complete folders, and retries
reuse installed packages. Manually verify that cross-instance local-mod drops and
modpack instance creation remain responsive, suppress repeated actions and keyboard
shortcuts, block window close during disk work, and show a persistent log-linked error
on failure. Confirm existing unsaved orders survive catalog refreshes.

### Main-list contextual controls

- At 1180×812 and the 1100×700 minimum, test single and split layouts in English and Chinese. Action rows remain fixed-height when selections/counts change. Each active list names its target above its Enable/Disable controls.
- Select several inactive mods, then click Enable over the other active pane without focusing its list first. Confirm the named target receives that exact selection; Disable removes only its own pane's selection. Repeat with both views on the same instance. Double-click and drag keep their existing target behavior.
- In each search, test text-only, source-only, status-only and combined filters. The trailing × and Escape in the search/list clear the same query/source/status state, including a no-results list. Search field/view preferences remain intact. Ctrl+F (⌘F on Mac) focuses the current pane's search; Ctrl+Z while typing remains text undo. Escape in dialogs or combo menus must not reset a pane.
- Edit two different instances: each affected header says Unsaved, the bottom status counts both and states that Save applies to all edited instances, and Save changes shows its shortcut and emphasis. Review instances opens their full names and configuration paths. Save or discard removes badges/emphasis and closes an open review. Returning to an already-edited instance restores its badge.
- At 1100×700, edit 40 synthetic instances, including duplicate display names and 300-character names, in single/split layouts and all four languages. The banner must stay one line with the same height and usable list space. Instance selectors truncate instead of expanding, with full names available on hover. Review instances must remain inside the window, scroll to the last entry, preserve distinct configuration paths, allow copying full text, and dismiss with Escape/outside click without clearing pane filters. Tab/Enter opens it, arrow/Page Up/Page Down scroll, and focus returns to the button after dismissal. Reopen after saving/discarding or editing another instance to check for stale names. These are native checks, not assertions made by the console suite.

Local-mod deletion checks use generated temporary folders only. They cover cancelled
and delayed confirmation, captured targets after selection changes, mixed and total
failures, duplicate package IDs in separate locations, non-local target rejection,
and preservation of pending orders, saved configuration and surviving catalog rows.
Manually verify repeated delete commands open only one confirmation; Cancel leaves
files, selections and lists unchanged; disk work blocks window close and shortcuts;
partial failures show persistent counts and a working log action. Successful deletions
must preserve load orders (affected entries can become Missing), and must not remove
another copy of the same package from the catalog.

When the last catalog mod is deleted, instance selectors and pending orders must stay
visible; affected load orders should show their missing entries instead of resetting.

Missing-mod recovery links use display-only rows with localized, row-specific
accessible actions. Tests capture the intended URI without opening a browser,
reject unsafe schemes, credentials and control characters, and exercise opener
failures. Empty-pane checks distinguish missing-only load orders, absent game
folders, empty catalogs and filtered installed rows.

### Large missing/history lists

- With 1,000 or more fixture rows, Ctrl+F/⌘F focuses search; matching name, author,
  package ID, URL, status or history date updates the visible count. × and the first
  Escape restore all rows. A second Escape closes. No search writes saved data.
- Select a row with keyboard arrows, then Tab to its enabled recovery link;
  Shift+Tab returns to the row and Tab again leaves the list. Disabled links are
  skipped. Refresh/status changes retain search, unchanged row containers, and a
  focused link even when that row changes status. Never open synthetic external
  links during automated fixture checks.
- While history is filtered, Clear History must explicitly include hidden entries
  in its confirmation. Cancel retains the query and saved history; read failures
  retain displayed rows and disable unsafe clearing.

### Catalog search and target safety

- Open a large cached catalog. Search name, summary or ID with Ctrl+F/⌘F; verify
  visible/total counts and no-result guidance. Down/Enter from search must focus a
  selected result, visibly outlined, without activating any installation action.
- Filter the selected pack out: its details and install actions must clear. × or
  Escape from search/results restores the previous target, including after Refresh.
  A second Escape with an empty query closes. Escape outside search/results retains
  the existing dialog behavior. Do not confuse duplicate display names with identity.
- While installing/updating, catalog search/clear remain disabled. Cancel/reopen and
  background catalog updates must not change an active operation's target.


The export navigation checks cover all five stages in all four languages, explicit
local-save versus Close labels, invalid-stage rejection, retained/cleared token
and Workshop-link drafts, and strict local-save failure/retry for missing, null,
malformed and unreadable manifests. Native Linux checks additionally exercised the
production dialogs at 640×560, with isolated local-only save/validation/recovery.
These tests do not perform authenticated publication or grant token permissions.


UI action-wiring checks embed the current MainWindow markup and constructor source
to ensure Undo/Redo are not subscribed in both places. They also verify code-only
Language/Browse wiring and test the duplicate-detection rule against synthetic
examples. Native Linux two-edit tests verify the actual single-step history flow.


Pending-edit identity checks cover no-op/revert/redo, saved-baseline changes,
independent instances, order and duplicate occurrences, normalized IDs, version
and expansion changes, official/scratch modes, and missing/corrupt/unreadable file
comparisons. Native checks additionally verify badge/banner recovery, clean close,
and unchanged config bytes and timestamps when returning to saved state.
