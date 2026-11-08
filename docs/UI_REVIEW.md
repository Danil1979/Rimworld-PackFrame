# UI and workflow review

## Scope and status

This is an iterative review of PackFrame's desktop workflows. Passing checks below
are evidence for specific behavior, not a claim that all platforms, languages, or
live publishing/install scenarios have been validated.

The latest November 2026 checkpoint builds cleanly in Release and passes 877 regression
checks. Native UI checks used Linux, synthetic instances, and a dialog host that
constructs the production windows. The actual application launcher was also
verified with mod rows and badges. It now bundles and registers Inter rather than
relying on a font being installed on the computer.

## Coverage

| Surface | Checked in this pass | Remaining checks |
| --- | --- | --- |
| Main mod lists | Explicit per-pane enable target, matching-instance views, X/Escape filter recovery, visible shortcuts, pending header badges, Ctrl+S disk save, typing undo, 1100×700 English layout; copy unsaved lists while source disk remains unchanged | Long real lists, populated four-language main-window pass, Windows/macOS |
| Confirmation | Long message fits, action buttons remain visible, safe initial focus and Escape | Extreme translated strings |
| Rename | Empty input stays open with an error; Escape from button focus cancels | Filesystem rename failures on each OS |
| Import mode | Full labels, unavailable-overwrite explanation, safe Cancel/Escape | Full native import matrix |
| Config folder | Invalid path error and focus, compact layout, named field | Native folder pickers on each OS |
| Language | Choices inspected, Escape support reviewed | Complete four-language walkthrough |
| Pending changes | Safe initial focus, wrapped message/actions inspected | Update-and-restart integration on each OS |
| Color palette | Native edit/cancel persistence, corrupt-file retention, repair/retry, selected-color + label commit, brush rollback, Undo/Redo and persistent error/Log | Four-language recovery layouts; Windows/macOS permissions and races |
| Collection target | Long names/identity readable; empty selection and disabled update reviewed | Live Steam-owned collection retrieval |
| Update choices | Separate file/order views, searchable virtualized files, retained decisions, bulk scope, 1,000-file native review, keyboard confirmation/cancel, all four languages and compact Japanese layout | Actual mixed keep/replace installation; no-conflict native confirmation remains unverified |
| Completion | Long action labels fit; Close/Escape | All installer completion variants |
| Index summary | Validation and keyboard behavior reviewed | Four-language keyboard walkthrough |
| Missing mods/history | Long identifiers, empty history, corrupt-file errors, repair/refresh recovery, retained rows after another failure, repeated safe confirmation cancellation | Native external-browser failure, platform permission recovery; larger-than-1,000 native list stress |
| OSS settings | Required/invalid field errors and focus, custom-port preview, disabling/cancel, scrolling; atomic failed-save preservation in checks | Four-language layout; live upload failure matrix |
| Diagnostic log | Missing-file state, disabled Open, Refresh/Close inspected; read errors retain prior content in checks | Native permission-denied log and external opener failures |
| Export wizard | Five stages at 640×560 in all four languages; local-only Save and close, required-name focus/recovery, missing-manifest failure and retry, explicit optional stages, scoped Close; draft/clear preservation in checks | Real Steam/Gitee/GitHub publishing, live partial network failure, native populated Workshop lists and successful-PR link rows |
| Modpack catalog | Offline bundled entries, unavailable details, named disabled install actions, proportional split and pinned actions; minimum 1000×650 in all four languages; loaded/no-cover switching, scroll reset and selected-pack footer | Online install/cancellation matrix; very large selected-pack mod detail lists |
| Application update | Resizable bounded notes/errors, pinned retry/cancel, all four languages at 480×420, injected failure/retry/cancellation and stale progress, release-opener recovery | Actual Windows download/install/restart and cross-platform browser failures |

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
  Live network installation/publishing remains a separate pending check.

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

## Recovery and validation checkpoint

- Local mod deletion snapshots exact confirmed paths and uses the shared write
  guard. Partial failures remain visible with counts and log access. Only successful
  paths leave the catalog; duplicate-package copies and saved/pending orders remain.
  Recursive deletion can partially fail; the UI says so. Unix symlinks are covered
  with generated-file tests, while Windows junction behavior remains unverified.
- A truly empty catalog keeps instance selectors, missing IDs and pending badges.
  Native Refresh/filter changes and Ctrl+S were verified using an isolated empty
  game fixture, not GUI deletion of real files. Missing-only load orders now point
  to the Missing action instead of claiming the saved list is empty.
- Native minimum-size empty-state layouts were checked in English, Japanese and
  both Chinese variants. This does not substitute for populated-list coverage.
- Missing mods and history offer localized, keyboard-accessible Open link actions,
  retain copyable addresses, and disable missing/unsafe web links. A display-only
  model leaves saved history unchanged. The 780×420 missing dialog fits. URI and
  opener-failure tests use injected handlers; no external link was activated.
- OSS fields validate supported endpoint/bucket formats before saving, preserve
  custom ports and show the actual destination host. Field errors clear as input
  is corrected. Save validates format, not network reachability or permissions.
- Credential guidance accurately describes unencrypted local OSS storage,
  retention when disabled, and clearing versus revocation. Publishing tokens stay
  in the open wizard, not app configuration. GitHub's public flow recommends
  public_repo, and Gitee index submission explains destination write access.
- Cached catalog selection now gives relevant installation guidance. Late detail
  results cannot overwrite newer operation results or errors.
- Regression total: 683. Live publishing, installation, credentials and user-file
  deletion were not exercised in native tests.

## Next passes

1. Review large catalogs and further recovery-list edge cases, including selection
   moving out of an active status filter and platform-specific keyboard behavior.
2. Continue populated main-list workflows beyond the four-language minimum-size
   and many-pending-instance pass, including reorder/undo and interrupted writes.
3. Continue export recovery beyond local-save failures, including native populated
   Workshop lists and successful-PR link rows without real submissions.
4. Exercise isolated online install/update/cancel/recovery and external openers only
   in explicitly authorized destinations. Windows/macOS-specific behavior remains
   unverified on the Linux review desktop.

## Large recovery lists and pending-instance review

- Missing/history lists now virtualize their rows, with a bordered search field,
  trailing ×, visible Ctrl+F/⌘F and Escape guidance, filtered counts and no-match
  feedback. Search covers names, authors, package IDs, links, states and dates.
  The first Escape clears a query; a subsequent Escape closes the dialog.
- Stable row wrappers update changed properties without replacing focused controls.
  Native tests with 1,000 synthetic entries verified search/refresh, row-specific
  accessible names, Tab from a row to its link, Shift+Tab back, and Tab out to the
  footer. A focused link stayed focused while its row alternated status. No real
  external link was opened. Minimum 780×420 layouts were checked in English and
  Japanese; full four-language recovery-dialog navigation remains unverified.
- Clear History explicitly warns that hidden search results are included. Native
  cancellation retained both the query and all 1,000 saved entries. Synthetic
  regression checks cover filtering and incremental reconciliation at 10,000 rows;
  these are not wall-clock performance benchmarks.
- The unsaved banner no longer expands with every instance name. It shows a compact
  count and explicit all-instance Save scope, with a keyboard-accessible Review
  instances flyout containing complete names and distinct configuration paths.
  Long instance selectors truncate while their tooltips retain complete names.
- A real MainWindow fixture with 40 pending instances, duplicate names and a
  300-character name was verified at 1100×700 in English, Japanese, simplified and
  traditional Chinese. The lists stayed usable. Keyboard review, PageDown,
  Escape/focus return, saving all, and a subsequent one-instance edit were checked.
  The new review showed only the newly edited instance. Native copy-to-clipboard
  and discard flows were not exercised in this pass.
- Empty-catalog filter reset now refreshes both header counts and status-filter
  appearance. Native checks retained two missing IDs and the pending badge, then
  saved both IDs through Ctrl+S.
- Clean Linux Release build: zero warnings/errors; 717 regression checks passed.
  Windows/macOS and live network transactions remain unverified in this pass.

## Searchable catalog checkpoint

- The catalog now filters by pack name, summary and stable ID, with a trailing ×,
  visible Ctrl+F/⌘F and arrow-key guidance, and visible/total counts. Down or Enter
  moves focus from search into a result with a visible focus border.
- Filtering out a selected pack clears its detail/installation state. It does not
  silently select another target. Clearing search restores the prior selection by
  ID (or file/mirror identity for older entries), never by a duplicate display name.
- Catalog refresh retains the query and matching selection. No-result refresh stays
  unselected, with install/advanced actions disabled and clear recovery guidance.
  Search is disabled during an active operation and adds no installation/network
  side effects of its own beyond ordinary selected-detail loading.
- Native tests used 1,000 cached synthetic entries at 1000×650: one result out of
  1,000, summary search returning 500, hidden selection, no results, clear/restore,
  Refresh, Down/Enter, and actual keyboard focus. English, Japanese and both Chinese
  layouts fit. No fixture was installed, published or opened externally.
- Clean Linux Release build: zero warnings/errors; 727 regression checks passed.
  The new pure search/identity checks include 10,000 rows. Cross-platform native
  behavior, active-download GUI filtering and real online operations remain pending.


## Export navigation and local-save checkpoint

- Each export page now identifies its stage and purpose. Publishing/catalog stages
  are explicitly optional, Next names its destination, and the final action is
  Close rather than a misleading Finish beside the separate Submit button.
- The first page offers Save and close for local-only work. Both local-save routes
  require a name and keep the dialog open with visible, copyable error feedback if
  saving fails. Early Close on later pages keeps completed work but does not save
  unsent form fields; its accessible help and tooltip explain that scope.
- Missing, null, malformed or unreadable manifests now fail instead of silently
  returning success. Valid metadata retains the existing atomic-write path.
- Intentional clears and edits in index-token and Workshop-link drafts survive
  Back/Next; untouched drafts can still follow their source. Empty Workshop lists
  no longer reserve a large blank box. Publishing action groups wrap rather than
  requiring one wide horizontal row.
- Native checks covered all five stages at 640×560 in English, Japanese, simplified
  and traditional Chinese. Token guidance and lower actions remained reachable by
  scrolling; footer navigation stayed visible. English empty-name validation
  returned focus to the name, correction removed the message, and Save and close
  wrote the isolated manifest. Temporarily moving only that synthetic manifest
  made saving fail visibly; restoring it allowed retry without losing the form.
- The before/after comparison uses the same 760×720 English GitHub stage. No
  credential was entered, real service called, or fixture uploaded/submitted.
  Successful remote-operation rows, real service failure/cancellation and
  Windows/macOS remain unverified.
- Clean Linux Release build: zero warnings/errors; 757 regression checks passed.


## Single-step Undo/Redo checkpoint

- Toolbar Undo and Redo no longer subscribe twice to the same click. Their XAML
  handlers remain the single wiring point. A source-wiring regression check embeds
  the exact MainWindow XAML and constructor source used by the test build and
  detects the duplicate pattern while preserving valid code-only buttons.
- Native before/after reproduction used the same isolated two-instance fixture and
  two separate Enable actions. Before the fix, one Undo changed the active count
  from six to four and one Redo restored both edits. After the fix, individual
  Undo clicks produced six → five → four and Redo clicks four → five → six.
- Ctrl+Z and Ctrl+Shift+Z matched toolbar behavior. A new edit cleared redo, and
  undo/redo of a second-instance edit left the first instance unchanged. Ctrl+S
  persisted the expected distinct package IDs in both synthetic instance files,
  including the pre-existing unresolved ID.
- Clean Linux Release build: zero warnings/errors; 764 regression checks passed.
  This pass exercised Enable history, not drag reorder, color undo or ten-entry
  boundary behavior in the native app. Those remain separate follow-up checks.


## Accurate unsaved-state checkpoint

- Returning a staged list to the current saved contents now clears that instance's
  Unsaved badge and removes it from the compact pending summary. Redo marks it
  edited again. The comparison reads the current file rather than relying on an
  obsolete undo baseline, and includes order, duplicate occurrences, game version
  and known expansions.
- Missing, malformed or unreadable files cannot be mistaken for a saved empty
  list by this comparison. It never writes the configuration. Existing save and
  reload error handling remains separate; concurrent external-write conflict
  handling is not a new guarantee of this change.
- Native matching before/after checks disabled Allow Tool and then undid the edit.
  The prior build still showed Unsaved; the revised build removed it. With edits
  in two instances, successive Undo actions reduced the pending count from two to
  one to none, without changing the other instance's list. Redo restored pending.
- Closing after reverting to saved state required neither a Save nor a discard
  prompt. Both synthetic configuration files retained exactly the same bytes and
  modification timestamps. The pre-existing missing package IDs were retained.
- An older import-isolation test now stages a genuinely new second-instance edit
  after its earlier Save, rather than expecting a no-op to count as pending.
- Clean Linux Release build: zero warnings/errors; 783 regression checks passed.
  Windows/macOS, live external-write races and interrupted multi-file saves remain
  separate verification work.


## Retained drafts and safe recovery checkpoint

- Save validates every pending instance before writing mod-name lists or staged
  configuration. Newly malformed, missing or unreadable targets block saving;
  other edited instances are not written by that failed preflight. This protects
  against detected unreadable files, not arbitrary concurrent external changes or
  failures occurring after validation during a multi-file save.
- Failed discovery no longer drops the affected staged list. Pending edits remain
  in memory, blocked until a healthy reload reads both configuration and metadata.
  Repairing only one broken file does not release the block. Explicit discard or
  removal still clears the relevant draft and its block.
- Refresh now retains pending mod lists rather than demanding Save or Discard
  before rereading the catalog. This also makes repair followed by Refresh possible
  without first losing the retained draft. Refresh still resets Undo/Redo history.
- A persistent, localized notice identifies the retained-edit and blocked counts,
  explains repair + Refresh, and tells the user to keep the window open. The review
  marks affected instance paths. Damaged panes explain their read-only state rather
  than incorrectly suggesting that the user enable mods into an empty instance.
- Draft retention is in-memory recovery, not crash-safe autosave. Closing still
  requires an explicit pending-change choice; failed Save does not close the app.
- Native Linux matched-flow checks edited two synthetic instances, corrupted one
  configuration and invoked Ctrl+S. The baseline silently replaced the corrupt
  file. The revised build retained both drafts, showed read-only/recovery guidance,
  and left every instance file's bytes and modification timestamp unchanged.
- Both ordinary and blocked Refresh retained the staged lists. Closing while
  blocked still prompted; choosing Save failed safely and left the window open.
  After restoring the synthetic file, Refresh recovered both edits without writing;
  Ctrl+S then saved the expected lists, including the pre-existing unresolved ID.
- Native visual checks covered English, Japanese, simplified and traditional
  Chinese at 1180×812. The persistent notice wrapped without covering Save/Launch;
  the pending review identified the blocked folder separately from the healthy one.
- Clean Linux Release build: zero warnings/errors; 818 regression checks passed.
  Windows/macOS, crash recovery, live concurrent-file races and write failures
  after preflight remain unverified by this checkpoint.


## Scalable update-review checkpoint

- Local file decisions and mod/load-order changes have separate tabs. Added and
  removed counts stay in the tab label; a pinned summary shows all file decisions
  and the load-order choice before Apply. The default use-update behavior remains.
- File choices use stable row objects and a virtualized list. Search is view-only:
  hidden decisions remain in the confirmation. Keep all local and Use update for
  all explicitly apply to all conflicts, including hidden results. Their help text
  explains this scope. Reopening starts a fresh review rather than reusing a draft.
- Ctrl+F/Command+F switches to file search, Down/Enter focuses a result, Space
  toggles it, and Tab leaves the list instead of visiting every checkbox. Escape
  clears a focused search first; with an empty search it cancels. Apply is not an
  Enter default, and Enter on a no-result search does not confirm the update.
- Native 1,000-file checks showed only visible row containers, filtered to the last
  file, kept every file, then changed that one exception by keyboard. Confirmation
  returned 999 retained paths. A separate 25-file flow retained a hidden file and
  the current order across search clearing and tab changes, returning both choices.
- Mouse checkbox changes, both bulk directions, no-result recovery, cancellation,
  and no-conflict layout/cancellation were checked. The optional no-conflict Apply
  click was not executed, so that exact native confirmation remains unverified;
  its result semantics are covered by regression checks. No native review test
  invoked the pack installer, wrote a real instance, or accessed a live service.
- Matching before/after screenshots use the same 650×480 plan with 25 conflicts
  and 13 mod changes. Native layouts cover English and both Chinese variants at
  650×480, and Japanese at 480×420. Actions and summary remained visible.
- Clean Linux Release build: zero warnings/errors; 835 regression checks passed,
  including a 10,000-file state test. Actual installation after confirmation,
  platform screen readers, Windows/macOS and live update failures remain separate.

## Color settings and brush recovery checkpoint

- A failing regression reproduced stale cached color data replacing a newly
  malformed settings file. Display reads still tolerate unreadable data, but
  label, brush, Undo/Redo and selected-color mutations reread the current file
  strictly and refuse malformed, null or directory targets. Repair followed by
  retry is supported; unrelated current mod colors are preserved.
- Palette choices commit before closing. A failed save keeps the dialog open,
  retains edited labels and shows a persistent, wrapping error with the settings
  path. Cancel starts focused and saves nothing. The hint explains when labels
  are saved. Selected colors and labels use one atomic file replacement instead
  of separate writes; prior selected colors are captured for Undo in that commit.
- A failed brush stroke rolls back its visible preview, stops the brush and adds
  no Undo entry. A persistent Close/Log dialog explains what failed and how to
  retry, rather than relying on the existing one-second toast. Log opens the
  recorded failure details without launching an external application.
- Native Linux checks used only synthetic instance files. Palette failure kept
  an edited label and unreadable bytes intact; repair and retry saved it while
  preserving another mod's color. Cancel left the settings file byte-identical
  and reopening restored the saved label. The selected-mod context-menu palette
  also passed failure, repair/retry, combined label/color save and color Undo.
- Brush failure restored the old appearance and left Undo disabled. A successful
  stroke, Undo and Redo wrote the expected selected color without disturbing
  another mod. The final persistent error, Close-focused state and Log navigation
  were checked visually. No real game, installer or online service was invoked.
- Clean Linux Release build: zero warnings/errors; 852 regression checks passed.
  This checkpoint's native color recovery checks used English at 1180×812 with
  an 840×560 palette. Other languages have localized recovery strings but were
  not visually rechecked here. Cross-process write races, platform screen readers,
  Windows/macOS and disk failures after validation remain separate coverage gaps.

## Focused keyboard workflows checkpoint

- Native baseline testing reproduced Down on a source-filter button unexpectedly
  selecting the next mod and pulling focus into the list. Mod navigation now
  requires list focus; other controls retain their own arrow and Enter behavior.
  Modified arrows are not mistaken for plain list navigation.
- Down or Enter in a mod search flushes its pending query and focuses a matching
  result without enabling/disabling anything. Empty results keep search focused.
  Only a separate plain Enter within the list performs the existing toggle action.
  Search fields show a down-arrow affordance and localized keyboard help.
- Shift+F10 and the Menu key open the existing mod actions beside the selected row,
  preserving multi-selection. Escape returns focus to the list. Keyboard opening
  does not depend on the desktop pointer's position or invoke an action itself.
- Native Linux verification retained two selected mods through keyboard menu
  navigation, Color, and palette cancellation. Search-to-result Enter caused no
  dirty state; a second Enter enabled the intended mod and Undo restored the saved
  list. No-result Enter/Down stayed in search; Escape restored results. The original
  filter-button arrow-focus issue did not recur. No external menu action was run.
- Clean Linux Release build: zero warnings/errors; 873 regression checks passed.
  Native checks used English and synthetic files. Other-language help is localized,
  but macOS modifier conventions, platform screen readers, touch and platform
  native context menus remain unverified by this checkpoint.

## Application-update dialog recovery checkpoint

- The dialog is resizable with a 480×420 minimum. Release notes and error details
  scroll independently, while progress and Later/Cancel/Retry remain outside both
  scroll regions. Status text is selectable and progress has a localized name.
- Failed downloads offer an explicit Retry download action and connection, disk
  and permission guidance. Each attempt owns its progress callback; delayed
  reports from a prior failed attempt cannot overwrite the running retry. A
  canceled or closed dialog does not accept a late prepared result.
- The release action now uses a validated web opener that propagates failures to
  the dialog. A failed opener leaves a copyable URL and keeps the dialog usable.
  This fixes the production path's previously swallowed launcher exception.
- Native Linux checks used injected local callbacks, with no live network request,
  external browser launch, archive write or installer execution. A 50-line failure
  fit at 480×420 in English, Japanese, simplified and traditional Chinese; scrolling
  to the last error did not hide the footer. Retry reached ready state.
- A delayed old-attempt preparation message was injected while a 15-second retry
  remained in its verification phase. The displayed current phase stayed correct
  and later reached ready. Closing ready returned no install result; Cancel during
  a separate pending callback also closed without an install result. The Install
  and restart action itself was not exercised. Production platform gating is
  unchanged; the fixture supplies its own capability and preparation callbacks.
- Clean Linux Release build: zero warnings/errors; 877 regression checks passed,
  including safe release URL handling and opener-error propagation. Actual Windows
  installation/restart, live download failures and platform screen readers remain
  unverified by this checkpoint.
