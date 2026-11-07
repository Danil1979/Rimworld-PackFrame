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
List tests cover contiguous/noncontiguous moves, insertion boundaries,
case-insensitive deduplication, two-instance undo/redo, and missing undo targets.
Launch tests inspect arguments without launching RimWorld; installer tests use
temporary mod/config files without downloading anything.
Catalog checks use an injected HTTP handler to cover offline bundled startup,
ETag/304 and Last-Modified checks, changed hashes, persistent and corrupt caches,
source isolation, mirror-relative URLs, cancellation, and unwritable cache paths.
Pack-detail checks cover revisits without another fetch, restart persistence,
mirror aliases, mutable-model isolation, changed hashes, and failed/corrupt reads.
Failure checks cover damaged-profile isolation, stale pending edits, installation
rollback and staging-path rejection, recovery across damaged manifests, mutable
color snapshots and failed color writes. Linux/macOS CI additionally constructs
a tar.gz and checks executable permissions through the actual SteamCMD extractor.

Manual platform checks:

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
- Select the same instance in both enabled panes: both show "Same list", with
  independent search and scroll positions. Select different instances (including
  instances with identical display names): the badges disappear. Change language
  and confirm the badges and launch target are translated.
- Drag within a list or between views of the same instance: the destination is
  outlined, the ghost says "Move here", and the insertion line follows the drop
  position. Drag between different instances: it says "Add to <name>" and leaves
  the source intact. Drag to the inactive pane: it says "Disable in <name>".
  Leaving a valid list, releasing the pointer, or losing pointer capture must
  clear the destination outline and action label.
- Click either enabled pane and change its instance: the launch button names that
  target. Check long names at the minimum window width; the complete launch name
  remains available in its tooltip and accessible name.
- Drag a contiguous selection within itself, move a noncontiguous selection to
  the beginning/end, drag between two profiles, and copy into a profile that
  already contains an item. Check the selection after each operation.
- Undo and redo a cross-profile move and a color change. Check Ctrl+Z, Ctrl+Y,
  Ctrl+Shift+Z and the toolbar buttons; new edits must clear redo history.
- Keep testing Shift/Ctrl selection, double-click enable/disable, and drag
  insertion with a search filter. These event/coordinate paths remain in the UI.

- Search quickly in each column: only that column should rebuild after 180ms;
  other columns clear selection without rebuilding. Details and keyboard actions
  must target the searched column; zero results must clear details. Switch the target instance and search
  again to check the inactive list uses the new target.
- Select mods rapidly: the preview must match the latest selection, including
  switching to a mod without an image. Close while an image is loading.
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
