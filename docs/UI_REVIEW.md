# UI and workflow review

## Scope and status

This is an iterative review of PackFrame's desktop workflows. Passing checks below
are evidence for specific behavior, not a claim that all platforms, languages, or
live publishing/install scenarios have been validated.

The November 2026 checkpoint builds cleanly in Release and passes 499 regression
checks. Native UI checks used Linux, synthetic instances, and a dialog host that
constructs the production windows. The actual application launcher was also
verified with mod rows and badges. It now bundles and registers Inter rather than
relying on a font being installed on the computer.

## Coverage

| Surface | Checked in this pass | Remaining checks |
| --- | --- | --- |
| Main mod lists | Selection, search recovery, pending edits, copying unsaved lists while source disk remains unchanged, safe delete cancellation | Long real lists, minimum-size pass, Windows/macOS |
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
| Missing mods/history | Long identifiers and empty history inspected | Actionable links, large-list performance, durable history error handling |
| OSS settings | Scrolling inspected; named fields and failed-save preservation covered in code/tests | Stronger syntax validation; live upload failure matrix |
| Diagnostic log | Missing-file state, disabled Open, Refresh/Close inspected; read errors retain prior content in checks | Native permission-denied log and external opener failures |
| Export wizard | All five pages inspected; custom title and repository URL survive Back/Next; intentional clearing survives navigation | Real Steam/Gitee/GitHub publishing, partial network failure, all languages |
| Modpack catalog | Offline bundled entries, unavailable details, disabled installation inspected | Responsive layout, large catalog, online install and cancellation matrix |
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

## Next passes

1. Make the modpack catalog responsive, remove blank cover space, and make install
   actions and their accessible names easier to understand.
2. Move remaining large local-copy/final-instance-creation operations off the UI
   thread and unify their busy/failure feedback.
3. Harden missing-history persistence and large-list navigation without losing
   selection during live refreshes.
4. Review credential guidance, endpoint validation, focus order, and error recovery
   across all export/setup pages.
5. Repeat minimum-size, four-language, keyboard, and platform-specific walkthroughs
   after each substantive change. Test live external operations only in explicitly
   authorized isolated destinations.
