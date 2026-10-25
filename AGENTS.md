# AGENTS.md

This is the repository-level working agreement for AI coding agents. Read it before changing code. If your tool has its own instruction file, treat it as a pointer to this file, not as a second source of rules.

QuickText is a **Windows-only WPF / .NET 10** tray application: a snippet manager and text expander. It is distributed as a portable executable. There is no web stack and no plugin system. User-facing documentation is `README.md`, `README.zh.md` and the translations under `docs/i18n/`; release notes live under `docs/release-notes/`. Build instructions are in the README’s **Build & run** section.

## 1. Read before changing

- **`README.md` / `README.zh.md` / `docs/i18n/`** — what the app promises and what each screen does.
- **`docs/release-notes/`** — historical release content. Do not rewrite a published release note; add a new version’s note only when cutting that release.
- **`.github/workflows/ci.yml`** — the real test, smoke, publish, checksum and release gates. Do not infer a release guarantee from a green local build.
- **The WPF-specific skill**, if available (`wpf-desktop`) — especially its pitfalls and dev-switch references. Read the matching section before touching layout, DPI, activation, tray behavior or the test harness.
- **`git status` and `git log`** — the working tree may contain uncommitted work. Never treat it as a clean release tree.

## 2. Build and verify

Run the narrowest useful check first, then the full gate:

```bash
dotnet build QuickText.sln -c Debug
dotnet test tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet build src/QuickText.App -c Release
```

The app has two in-process self-checks:

```bash
# Constructs and lays out every XAML window; must be run after XAML or window-option changes.
QuickText.exe --smoke

# Renders the UI matrix (languages, themes, short screens) to a directory.
QuickText.exe --shots <directory>
```

- `--smoke` writes a marker to the temp directory; CI treats a missing or non-`OK` marker as failure even when the process exits 0.
- `--shots` is a screenshot harness, not a substitute for a screen-level check. It sets `MaxHeight` itself and renders windows offscreen; a dialog missing its own runtime cap can look fine in every image, and a button can be rendered outside the real monitor. For a scrollable dialog or a window with a screen cap, also verify the primary action’s screen rectangle or take a full-screen capture.
- Use an isolated copy/portable data folder for UI checks and never disturb a running user instance. A second launch is an explicit user entry that hands off to the running instance (only autostart launches stay silent); do not use it as a test harness.
- Do not run a harness that installs a real global mouse hook or a full-screen topmost overlay on the machine you are using. Read the WPF skill’s warnings first.

## 3. WPF and layout invariants

- The process is **PerMonitorV2** DPI-aware (`src/QuickText.App/app.manifest`). Never assume one DPI for the whole desktop. Use the window’s actual monitor/work area.
- Do not hardcode a window or content height that must serve both a small laptop and a 4K display. Derive it from the work area of the monitor the window is on; keep a conservative XAML fallback for when monitor information is unavailable.
- Every `SizeToContent` window must stay bounded on a short screen: keep its runtime height cap, and when you touch or add one, include a conservative XAML `MaxHeight` fallback. A window-level cap is not automatically a content cap: a scrollable body can still lay out past the work area. Keep the title and action buttons outside the scroll area.
- Do not bypass the existing foreground / z-order guards. In particular, never use `SetWindowPos` on a `Topmost` window; read the WPF skill’s pitfall reference before changing activation.
- Keep the single-instance, config-location and startup behavior intact. A test that needs a second instance must use an isolated copy or a dedicated message name; it must not disturb a running user instance.
- Put first-run defaults in the settings default or the relevant seed, not in a model property that existing configs rely on. Existing settings files may omit a field; a missing field must keep its previous behavior. Test both a fresh config and an old config with the field absent.
- The application is offline by design. The update check is the only network path and is opt-in. Do not add telemetry, cloud calls or silent background network behavior.

## 4. Localization and documentation

- The neutral `src/QuickText.Core/Localization/Strings.resx` is the Simplified Chinese source. Every UI key also exists in the satellite resx files. Add a new key to **all** satellite files; never leave a raw key or an empty translation.
- Keep the localized READMEs structurally aligned with the root READMEs. `README.zh.md` and every `docs/i18n/README.*.md` use the same section set as `README.md` (the Download heading included); translations should not drift into old sections or omit current ones.
- User-visible behavior changes land with their documentation in the same change. Update the English and Chinese READMEs, then the translations when the change belongs in the user-facing documentation.
- Code comments explain **why**, not what. Do not write drifting line numbers, test counts, release versions or “this round” notes into comments or docs. Historical references to removed code are allowed when the surrounding text says so.
- Do not rewrite historical planning/report files under `.superpowers/sdd/` or local preview notes; they are records, not current product documentation. Do not commit build outputs, screenshots, temporary configs, local settings or `.qoder-credits/`.

## 5. User data and safety

- User configuration and state live next to the portable exe or under `%APPDATA%\QuickText\`; the library defaults to `Documents\QuickText` in installed mode. Tests must not edit the user’s real settings, library, startup entries or running instance.
- Destructive actions keep their existing confirmation and honest failure reporting. Do not silently swallow an error in a user-triggered flow.
- Do not add a confirmation dialog, extra step or notification unless it removes a demonstrated confusion or risk. Prefer the smallest fix in copy, defaults, validation or feedback.

## 6. CI and releases

- The git tag is the version source. A source build takes the newest reachable tag; CI passes the tag to `dotnet publish`. Do not maintain a second version number in the app or docs.
- Read `.github/workflows/ci.yml` before changing CI. It runs the test floor, a window smoke check, both single-file publishes, checksum/attestation and the release job. A release is not proven by a green local build; verify the latest release and every downloadable asset separately.
- Do not add Dependabot. Dependency versions are checked manually before a release with `dotnet list package --outdated`, `dotnet list package --vulnerable --include-transitive`, and the Action major check.
- The winget manifest is owned by the external `rockbenben/winget-manifests` repository. Do not submit a second manifest from this repository; doing so opens a duplicate PR.
- Do not push, create a tag, publish a release or edit published assets without explicit maintainer approval. A local commit is not a release.

## 7. Working with the tree

- Keep changes minimal and match the surrounding style. Do not add abstractions, settings or mechanisms for hypothetical future needs.
- Separate commits by feature domain. Do not stage unrelated dirty files, generated screenshots, logs, local settings or the user’s configuration.
- Do not rewrite historical spec/report files under `.superpowers/sdd/`; do not add dependencies or generated assets without a reason that is in scope.
- Run `git diff --check` before reporting a change.

## 8. Definition of done

Before reporting a change as complete:

1. Build and the relevant tests pass.
2. `--smoke` passes; `--shots` passes when UI or layout changed, plus a screen-level check where the harness has a blind spot.
3. Localization keys exist in every satellite resx file; the README section structure remains aligned across languages.
4. Documentation and comments describe the current behavior, with no stale line numbers, counts or version notes.
5. `git diff --check` passes and the report states what was verified, what was not, and whether anything was pushed or released. Never claim a visual or release check that was not actually run.
