# UI and workflow review

## Scope and status

This is an iterative review of PackFrame's desktop workflows. Passing checks below
are evidence for specific behavior, not a claim that all platforms, languages, or
live publishing/install scenarios have been validated.

The November 2026 checkpoint builds cleanly in Release and passes 559 regression
checks. Native UI checks used Linux, synthetic instances, and a dialog host that
constructs the production windows. The actual application launcher was also
verified with mod rows and badges. It now bundles and registers Inter rather than
relying on a font being installed on the computer.

## Coverage

| Surface | Checked in this pass | Remaining checks |
| --- | --- | --- |
| Main mod lists | Explicit per-pane enable target, matching-instance views, X/Escape filter recovery, visible shortcuts, pending header badges, Ctrl+S disk save, typing undo, 1100×700 English layout; copy unsaved lists while source disk remains unchanged | Long real lists, all-language main-window pass, Windows/macOS |
| Confirmation | Long message fits, action buttons remain visible, safe initial focus and Escape | Extreme translated strings |
| Rename | Empty input stays open with an error; Escape from button focus cancels | Filesystem rename failures on each OS |
| Import mode | Full labels, unavailable-overwrite explanation, safe Cancel/Escape | Full native import matrix |
| Config folder | Invalid path error and focus, compact layout, named field | Native folder pickers on each OS |
| Language | Choices inspected, Escape support reviewed | Complete four-language walkthrough |
| Pending changes | Safe initial focus, wrapped message/actions inspected | Update-and-restart integration on each OS |
| Color palette | Layout inspected; cancel/discard semantics and field names covered in code/tests | Native edit/cancel persistence matrix |
| Collection target | Long names/identity readable; empty selection and disabled update reviewed | Live Steam-owned collection retrieval |
| Update choices | Conflict semantics explicit; actions pinned outside scrolling area | Native mixed keep/replace update against isolated installation |
| Completion | Long action labels fit; Close/Escape | All installer completion variants |
| Index summary | Validation and keyboard behavior reviewed | Four-language keyboard walkthrough |
| Missing mods/history | Long identifiers, empty history, corrupt-file errors, repair/refresh recovery, retained rows after another failure, repeated safe confirmation cancellation | Actionable links, large-list performance, platform permission recovery |
| OSS settings | Scrolling inspected; named fields and failed-save preservation covered in code/tests | Stronger syntax validation; live upload failure matrix |
| Diagnostic log | Missing-file state, disabled Open, Refresh/Close inspected; read errors retain prior content in checks | Native permission-denied log and external opener failures |
| Export wizard | All five pages inspected; custom title and repository URL survive Back/Next; intentional clearing survives navigation | Real Steam/Gitee/GitHub publishing, partial network failure, all languages |
| Modpack catalog | Offline bundled entries, unavailable details, named disabled install actions, proportional split and pinned actions; minimum 1000×650 in all four languages | Loaded artwork, large catalog, online install and cancellation matrix |
| Application update | Unavailable-asset state and release notes inspected | Long errors, small screen, real isolated replacement/recovery |

## Regression protections added

- Instance writes are guarded against reentry, run outside the UI thread, and
  retain pending edits when an operation fails. Workshop refreshes defer across
  writes and discard results that span an instance mutation.
- ZIP import validates the mod list, XML configuration, and metadata before
  replacing a profile. Official-config replacement rolls back recursively without
  removing unrelated files. Managed-profile backups remain until validation passes.
- OSS settings apply to the live configuration only after persistence succeeds.
- Config commit cannot be interrupted by closing its window. Cancelled installs do
  not report success after an uncancellable tail step.
- Export operations share one gate before preparation starts. A successful index
  destination is not submitted again when another destination is retried. History
  is scoped to the open wizard, not persisted across restarts.
- GitHub manifests retain their actual branch URL. Public index submission strips
  URL query/fragment credentials and verifies the manifest anonymously; private or
  inaccessible manifests cannot contribute authenticated metadata to an index.
- Palette cancellation no longer persists label changes. Diagnostic log errors are
  distinct from a genuinely empty/missing log.

## Follow-up: history recovery and catalog layout

- History mutations use strict reads and flushed atomic replacement. Corrupt or
  unreadable history is not silently replaced with an empty document.
- History read/clear failures retain rows, stay visible, and offer Refresh. The
  main-window history action remains reachable when the file cannot be read.
- Catalog/details use proportional columns. Missing art no longer leaves a blank
  panel, install actions stay outside the detail scroll, and composite actions
  expose localized accessible names. Native checks confirmed controls remain
  reachable at 1000×650 in English, Japanese, simplified and traditional Chinese.
- The full suite now includes 39 additional history regression checks (538 total).
  Live network installation/publishing and loaded-cover behavior remain separate
  pending checks.

## Main-list revision after user feedback

- Enable/Disable controls now sit directly above each destination list. Their tags
  identify the source/target explicitly instead of inferring the action from focus.
- Each search has one trailing × for clearing its search/source/status filters.
  Escape clears the focused pane, while Ctrl+F/⌘F is visible beside the input.
  The redundant Reset and empty-state Clear filters buttons are removed.
- Edited instances show an Unsaved badge in their own header. The Save changes
  action includes its shortcut, and a banner names all pending instances.
- Native tests verified the left instance alone is edited when its Enable button
  is clicked after choosing a different right instance. Same-instance views stay
  synchronized. Typing undo leaves staged mod edits intact; Ctrl+S persists them.
- Final pack creation and cross-instance local copies use the shared busy/close
  guard. Copy failures do not activate requested IDs or expose partial folders.
- Regression total: 559. The dedicated UI branch remains separate from the
  Workshop/download-fix branch; no main or PR-head changes are part of this review.

### Where to find the reviewed dialogs

- Rename and import: **Instance ▾** above an active list, then **Rename** or **Import**.
- Export/publishing: **Instance ▾ → Export** or **Upload**.
- Catalog and pack update options: **Modpacks**, beside **Instance ▾**.
- Availability/history: the **Missing** or history action below an active list.
- Diagnostics: **Log**, in the bottom-left footer.

## Next passes

1. Verify loaded cover art, large catalogs, and cached detail transitions; refine
   install-action wording and native interaction states.
2. Move remaining large local-copy/final-instance-creation operations off the UI
   thread and unify their busy/failure feedback.
3. Improve missing-mod/history links and large-list navigation without losing
   selection during live refreshes.
4. Review credential guidance, endpoint validation, focus order, and error recovery
   across all export/setup pages.
5. Repeat minimum-size, four-language, keyboard, and platform-specific walkthroughs
   after each substantive change. Test live external operations only in explicitly
   authorized isolated destinations.
