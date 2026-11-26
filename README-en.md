# PS1Run

> A small Windows desktop utility written in C# + WPF that takes over what happens when you double-click a `.ps1` file: run it, edit it, or ask you every time — and automatically pick between Windows PowerShell 5.1 and PowerShell 7 based on the script's encoding.

English | [简体中文](README.md) | [Changelog](CHANGELOG.md)

---

## Table of Contents

- [The Problem](#the-problem)
- [Screenshots](#screenshots)
- [Features](#features)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Command-Line Arguments](#command-line-arguments)
- [Configuration and Directories](#configuration-and-directories)
- [Logging and Diagnostics](#logging-and-diagnostics)
- [Project Layout](#project-layout)
- [Development and Testing](#development-and-testing)
- [Known Limitations and Roadmap](#known-limitations-and-roadmap)
- [Documentation](#documentation)
- [Security Notes](#security-notes)

---

## The Problem

On Windows, double-clicking a `.ps1` file is awkward by default. It either opens in Notepad, or it runs in PowerShell with a console window that flashes and disappears. And if PowerShell 7 is installed, GBK-encoded scripts often end up running under `pwsh` and printing garbled Chinese text.

PS1Run takes that behaviour back:

- Double-click a `.ps1` → **run it**, **open it in your editor**, or **show a small picker and let you decide on the spot** (the default).
- When running, it inspects the script encoding: UTF-8 / UTF-16 goes to PowerShell 7, ANSI/GBK goes to Windows PowerShell 5.1, so non-ASCII output stays readable.
- The picker states up front whether **PS5 or PS7** will be used, plus the reason — you can confirm before anything executes.
- If no editor is found, it falls back step by step: custom path → common install locations → registry → `PATH` → Notepad. It never dead-ends.

Everything is launched on demand. There is **no background resident process**; the app dispatches and exits.

> **Note:** Directory Opus cannot invoke PS1Run on double-click by default. It ships its own file-type double-click rules, which intercept the action before the Windows default association (`ShellExecute`) is consulted.

## Screenshots

**Main window · General (light · Simplified Chinese)** — the first of three tabs: run mode, executor & editor (PS5.1 / PS7+ / editor path), and one-click detection. The association badge stays in the title bar.

![PS1Run General tab: light theme, Simplified Chinese, run mode set to Ask, executor & editor group with PS5.1, PS7+ and a VS Code path, plus the one-click detection button](wiki/home-1.png)

**Main window · File association (light · English)** — association status card and registration actions. Per-user registration needs no elevation; machine-wide registration goes through UAC.

![PS1Run File association tab: light theme, English UI, showing registered status, the open/run/edit verbs, and buttons to register, unregister, open 'Open with', and open default apps settings](wiki/home-2.png)

**Main window · Logs & appearance (dark · English)** — log retention, appearance & language, and config file & version. The language pack version appears as `v2 · en-us.lang`.

![PS1Run Logs & appearance tab: dark theme, English UI, with log retention days, theme and language dropdowns, config file path, app version, and language pack version](wiki/home-3.png)

The association badge in the title bar is visible on all three tabs. When the Chinese language pack has no version number, the Logs & appearance tab also shows a red “!” badge and red notices appear both in the tab and in the picker window.

**Picker (light · Simplified Chinese)** — a UTF-8-with-BOM script is routed to PS7; interpreter, executable, and rationale are visible before anything runs.

![PS1Run picker: light theme, Simplified Chinese, FilesMove.ps1 showing the PS7 badge, pwsh.exe, the auto-detection rationale UTF-8 with BOM, and Cancel/Edit/Run buttons](wiki/picker-1.png)

**Picker (dark · English)** — an ANSI/GBK script is routed to PS5; the badge card and accent button stay legible on dark.

![PS1Run picker: dark theme, English UI, WPFPublish.ps1 showing the PS5 badge, powershell.exe, the auto-detection rationale ANSI/GBK, and Cancel/Edit/Run buttons](wiki/picker-2.png)

> The source screenshots live in the [`wiki/`](wiki) folder: `home-1.png` … `home-3.png` and `picker-1.png` … `picker-2.png`.

## Features

### File association

- Registers its own ProgId `Ps1Run.ps1` with three verbs:
  - `open` (double-click): dispatch according to your settings; show the picker in `Ask` mode;
  - `run` (context menu "Run"): force execution with an interpreter;
  - `edit` (context menu "Edit"): force the editor.
- Writes to `HKCU\Software\Classes` by default, so **no administrator rights are required**. Machine-level registration (`HKLM`) is optional, elevates through UAC, and reports clearly when it fails.
- Uninstall removes only the keys it created and de-registers itself from `OpenWithProgids`.
- Two guided entry points — "System 'Open with'…" and "Open default apps settings" — help you finish Windows' default-program switch. (`UserChoice` is hash-protected; the app never forces a rewrite.)

### Run modes

| Mode | Behaviour on double-click |
|---|---|
| **Ask every time** (default) | Shows the picker: Run / Edit / Cancel, with an optional "always do this" checkbox |
| **Run directly** | Immediately launches the selected interpreter |
| **Open in editor** | Immediately opens the detected editor |

The default is "Ask every time", because running an unknown script straight from a double-click is risky.

### Interpreter selection (PS5 / PS7)

- Three-state policy: `PowerShell 5` / `PowerShell 7` / `Auto` (default).
- In `Auto` mode the decision is driven by script encoding:

| Encoding | Selected |
|---|---|
| UTF-8 with BOM / UTF-8 without BOM / UTF-16 LE / UTF-16 BE | PowerShell 7 (`pwsh.exe`) |
| ANSI / GBK | Windows PowerShell 5.1 (`powershell.exe`) |
| Empty file / undetectable | Falls back to your preference, with a notice in the picker |

- Probe order:
  - PS5: `System32\WindowsPowerShell\v1.0` → `SysWOW64` → `App Paths` → `PATH`;
  - PS7: `PowerShellCore\InstalledVersions` registry → `App Paths` → `%ProgramFiles%\PowerShell\7` → `7-preview` → `PATH`.
- You can pin the PS5 / PS7 executable manually; a pinned path wins over detection and is still validated before use.
- Scripts run as `-NoProfile -File "<script>"` with the working directory set to the script's folder, so relative paths inside scripts behave as expected.

### Editor discovery

Five-level fallback: user-defined → common install paths (VS Code / Notepad++ / Sublime Text / PowerShell ISE / Notepad) → registry `App Paths` and `Uninstall` entries → `PATH` → system Notepad.

### UI, themes, and localization

- The main window is the single place to manage run mode, interpreter policy, editor, file association, theme, language, and logs.
- Themes: **Auto / Light / Dark**. Auto reads the system `AppsUseLightTheme` value and switches instantly.
- Localization: strings live in `lang\<code>.lang`. Simplified Chinese and English ship with the app; any additional file dropped into `lang\` is discovered by the dropdown, and switching updates already-visible text immediately — no restart.
- The picker shows the script name, full path, the **PS5/PS7 badge with its rationale**, and an amber warning when a fallback or a missing executable is involved. It also shows a red notice when a configured executor/editor path no longer exists (or no editor can be discovered), and opens centred on screen without an initial flash.

### Script rules (this script only)

The picker has a second checkbox, "Always do this for this script only". When checked, the script is saved to `rule.txt` using the current criteria; the next double-click in Ask mode dispatches directly without showing the picker. It is mutually exclusive with "Always do this (don't ask again)", and `--ask` always shows the picker regardless of rules.

- The file path is **always matched** and cannot be disabled.
- Optional criteria on the Script Rules tab: `File size` (on), `Modified time` (on), `File hash` (off by default; reads the whole script to compute SHA-256 and may slow startup).
- Matching short-circuits in the order path → size → modified time → hash; the hash is computed only when earlier checks pass.
- Saving rules in the UI takes effect immediately; no restart is needed.

**Script Rules tab (light · Simplified Chinese)** — criteria on top, rule list, and CRUD actions.

![PS1Run Script Rules tab: light Chinese UI, file size and modified time enabled, hash disabled, with path filter, rule grid, and refresh/add/edit/delete/save buttons](wiki/rules.png)

### Configuration and logging

- `config.lang` is the single configuration file: program configuration (log retention, theme, language) plus behaviour settings (run mode, interpreter policy, custom paths, rule criteria) and a schema `Version`. It is created on first run, written atomically, and backed up then rebuilt if corrupted.
- `rule.txt` stores "always for this script only" rules as five-column plain text, editable on the Script Rules tab.
- Logs roll daily into `log\yyyyMMdd.log`, with levels plus session and process IDs. Retention is configurable, and expired files are pruned on the first run of each day.
- The UI exposes "Open log folder", "View today's log", "Copy recent logs", and "Clean up now" for easy issue reporting.

## Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 / Windows 11 (x64) |
| Runtime | Either **.NET Framework 4.8** (bundled with Windows 10 1809+) or the **.NET 11 Desktop Runtime** |
| Build environment | .NET SDK 11.0+ (dual-target builds also need the .NET Framework 4.8 targeting pack) |
| PowerShell | At least Windows PowerShell 5.1 (built into Windows); PowerShell 7 optional |

Two sets of artifacts are produced — pick whichever matches what the target machine has installed:

- `Build\net48\Ps1Run.exe` — depends on .NET Framework 4.8, best compatibility;
- `Build\net11.0-windows\Ps1Run.exe` — depends on the .NET 11 Desktop Runtime.

## Getting Started

### Build

```powershell
# Dual-target build (net48 + net11.0-windows)
dotnet build Ps1Run.slnx

# Or use the scripts
.\scripts\build-all.ps1                                # Debug, both targets
.\scripts\build-all.ps1 -Configuration Release -Test   # Release + run tests
.\scripts\build-net48.ps1                              # net48 only
.\scripts\build-net11.ps1                              # net11 only
```

All output goes to `Build\<TFM>\`.

### Run

```powershell
# Open the main window
.\scripts\run.ps1

# Run the net11 / Release artifact
.\scripts\run.ps1 -Framework Net11 -Configuration Release

# Build first, then force-run a specific script
.\scripts\run.ps1 -Build -Intent Run -ScriptPath "D:\scripts\demo.ps1"

# Pass arguments through verbatim
.\scripts\run.ps1 -Arguments @("--open", "D:\scripts\a.ps1")
```

You can also just double-click `Build\net48\Ps1Run.exe`.

### First-run checklist

1. Open the main window and check PS5 / PS7 and the editor path under "General → Executor & editor". Click "Detect PS5 / PS7 / editor" to fill only empty or invalid paths — configured paths that still exist on disk are left untouched.
2. Click "Register" for per-user file association. Use "Register for all users" only if you want machine-wide behavior, and confirm the UAC prompt.
3. Double-click any `.ps1` to verify the picker appears; it shows which interpreter will be used.
4. To undo, click "Unregister", or change the default app back via "Open default apps settings".

## Command-Line Arguments

| Argument | Description |
|---|---|
| *(none)* | Show the main window |
| `--open <script>` | Dispatch according to settings; shows the picker when run mode is "Ask" (used by the `open` verb) |
| `--run <script>` | Force execution with an interpreter, bypassing the run-mode setting |
| `--edit <script>` | Force the editor |
| `--ask <script>` | Force the picker |
| `--settings` | Open the settings UI directly |
| `--register-machine` | Perform machine-level registration in an elevated process (internal) |
| `--unregister-machine` | Perform machine-level unregistration in an elevated process (internal) |
| `--help`, `-h`, `-?` | Show help |
| `--` | Treat everything after it as a literal path, supporting filenames that start with `-` |

Precedence: `--run` / `--edit` > `--ask` > `--open` > bare path > no arguments. Passing `--run` and `--edit` together is reported as a conflict.

Exit codes:

| Code | Meaning |
|---|---|
| `0` | Success, or cancelled by the user |
| `1` | Unexpected internal error |
| `2` | Invalid command-line arguments |
| `3` | Script file not found |
| `4` | No interactive desktop, window creation failed, or target program unavailable |

## Configuration and Directories

The app is portable by default: everything lives in the application root, i.e. the folder containing `Ps1Run.exe`.

| Path | Purpose |
|---|---|
| `config.lang` | The only configuration file: program configuration (log retention, theme, language) + behaviour settings (run mode, interpreter policy, custom paths) + schema version |
| `lang\zh-cn.lang` | Simplified Chinese language pack (shipped; rebuilt at startup when its version is missing or older) |
| `lang\en-us.lang` | English language pack (shipped, never overwritten automatically) |
| `rule.txt` | Per-script rules: `path|action|size|modified|hash` |
| `log\yyyyMMdd.log` | Daily rolling runtime log |

> **Breaking change**: legacy `config.json`, `%APPDATA%\Ps1Run\settings.json` and `lang\*.json` are no longer read, migrated, or deleted. Replace them manually using the formats below. Leftover files are harmless but ignored.

> If the application root is not writable (typically installed under `Program Files` without elevation), `config.lang`, `rule.txt`, `lang\`, and `log\` **all** fall back together to `%APPDATA%\Ps1Run\`, with a WARN entry in the log.

### config.lang

```text
Version: 2,
RunMode: "Ask",
PowerShellPolicy: "Auto",
CustomPs5Path: "",
CustomPs7Path: "",
CustomEditorPath: "",
LogDays: 7,
Theme: "Auto",
Language: "zh-cn",
RuleMatchSize: 1,
RuleMatchModifiedTime: 1,
RuleMatchHash: 0,
```

| Field | Value | Notes |
|---|---|---|
| `Version` | currently `2` | Config format version; shares the key/value text format with language packs. Legacy JSON configs are ignored |
| `RunMode` | `Run` / `Edit` / `Ask` | Double-click behaviour; `Ask` prompts every time (default) |
| `PowerShellPolicy` | `Ps5` / `Ps7` / `Auto` | Interpreter policy; `Auto` decides by script encoding |
| `CustomPs5Path` / `CustomPs7Path` / `CustomEditorPath` | executable path, empty when unset | Empty values fall back to the discovery chain |
| `LogDays` | 1–365, default `7` | Log retention in days; out-of-range values are normalized back to 7 |
| `Theme` | `Auto` / `Light` / `Dark` | `Auto` follows the system theme |
| `Language` | Language code, e.g. `zh-cn`, `en-us` | Maps to `lang\<code>.lang` |
| `RuleMatchSize` / `RuleMatchModifiedTime` / `RuleMatchHash` | `1` / `0` | Rule criteria; the file path is always matched, hash defaults to `0` |

### Custom language packs

Create a plain-text file such as `lang\ja-jp.lang` with key/value pairs, and the dropdown will pick it up automatically:

```text
Version: 2,
Button.Save: "Save settings",
Button.Browse: "Browse…",
```

- `Version` is the language-pack version (currently `2`) and is never treated as UI text. When it is missing or older than the expected version, the Logs & appearance tab shows a red “!” badge, a red notice appears inside the tab, and the picker window shows a red line explaining that some text may fall back to Chinese (the pack still loads, missing keys still fall back).
- Fix it with “Regenerate zh-cn.lang”, which rebuilds the Chinese pack from built-in strings. Other language packs are never overwritten automatically — add `Version` or update the text yourself.
- Missing keys fall back in order: current language → built-in Chinese → the key name itself, so nothing renders blank.
- The complete key set is available in `lang\zh-cn.lang`. Translate only the keys you need; the rest fall back automatically.
- Each `rule.txt` line is `path|action|size|modified|hash`; action is `Run` or `Edit`, disabled criteria are written as `0`, modified time is UTC ISO 8601 and the hash is lowercase SHA-256.
- UI text binds through `{loc:T Key}` and refreshes immediately when the language changes.
- XAML also carries a design-time helper, `d:DesignText.Text="Save settings"`: it only supplies placeholder text inside the designer, is skipped entirely at runtime, and real text still comes from the language pack.

## Logging and Diagnostics

- Location: `log\yyyyMMdd.log` (retention controlled by `LogDays` in `config.lang`).
- Levels: `DEBUG` / `INFO` / `WARN` / `ERROR`. Each line carries `[s:sessionId]` and `[p:processId]`, which makes multiple double-click launches easy to tell apart.
- What gets recorded: startup arguments and exe path, encoding detection result, chosen interpreter and probe source, target process PID, exit code; cancellations, argument errors, missing files, process launch failures (including Win32 error codes), corrupted settings/config, association registration and elevation, and unhandled exceptions.
- Cleanup: expired logs are deleted on the **first** run of each day, with a "daily cleanup" entry written. Later launches on the same day skip it.
- UI entry points: "Open log folder", "View today's log", "Copy recent logs" (last 150 lines), "Clean up now".
- Logging failures are always swallowed — they never affect the dispatch path.

## Project Layout

```
Ps1Run/
├─ Ps1Run.slnx                     # Solution (three projects)
├─ Directory.Build.props           # Dual target: net48;net11.0-windows, output to Build\<TFM>\
├─ Ps1Run.Core/                    # UI-free core (unit-testable)
│  ├─ Associations/                #   ProgId, register/unregister, elevation, UserChoice detection
│  ├─ Configuration/               #   AppPaths, AppConfig, ConfigStore
│  ├─ Dispatch/                    #   argument parsing, dispatcher, picker contract, exit codes
│  ├─ Editors/                     #   editor discovery and registry anchors
│  ├─ Infrastructure/              #   logging, process launch, path search, key/value text parser
│  ├─ Localization/                #   built-in Chinese strings, language directory scan
│  ├─ Rules/                       #   rule.txt I/O, fingerprints, rule matching
│  ├─ PowerShell/                  #   encoding detection, PS5/PS7 probing
│  └─ Settings/                    #   preference model and persistence
├─ Ps1Run.App/                     # WPF shell: windows, bindings, themes, i18n
│  ├─ Themes/                      #   Light.xaml / Dark.xaml
│  ├─ Localization/                #   LocalizationManager, {loc:T}, design-time text
│  ├─ Views/ ViewModels/ Services/ #   main window, picker, placement, theme manager
│  └─ lang/en-us.lang              #   English pack shipped with the app
├─ Ps1Run.Core.Tests/              # Dependency-free console test project (net11 single target)
├─ scripts/                        # Build and run scripts
├─ wiki/                           # Requirements, feature matrix, architecture, deep dives, progress
└─ Build/                          # Build output (net48 / net11.0-windows)
```

Design rule: all testable logic lives in `Ps1Run.Core`; `Ps1Run.App` only handles windows and process lifetime.

## Development and Testing

```powershell
# Build
dotnet build Ps1Run.slnx

# Unit tests (8 groups)
dotnet run --project Ps1Run.Core.Tests\Ps1Run.Core.Tests.csproj --no-build
```

The eight test groups:

| Group | Coverage |
|---|---|
| Encoding detection | Six input shapes (BOM / no BOM / UTF-16 / ANSI / empty / invalid) |
| Argument parsing | Verb precedence, conflicts, literal-path mode |
| Interpreter probing | PS5/PS7 probe chains and fallback order |
| Settings persistence | key/value text round-trip, defaults on missing values, backup-and-rebuild on corruption, legacy JSON ignored |
| Log write and retention | Level/session format, reading recent lines, retention policy, daily cleanup |
| Program configuration | `config.lang` generation, range normalization, corruption recovery |
| Localization resources | `.lang` directory scan, default pack generation, missing-key fallback, legacy JSON ignored |
| Script rules | five-column I/O, CRUD, mandatory path match, size/time/hash short-circuit, path validation |

Tests run against `net11.0-windows` only, to avoid doubling the matrix. The `net48` path is covered by the dual-target build plus manual startup verification.

## Known Limitations and Roadmap

- **System-level acceptance is still manual**: registry changes, double-click dispatch, and multi-monitor picker placement need a real desktop session and are not automated.
- **Uninstall rollback is incomplete**: unregistration clears the ProgId and `OpenWithProgids` entries it created, but does not restore the previous default value of `.ps1`, and there is no `association-backup.lang` (reserved, not implemented) snapshot.
- **Store edition of PS7 is not probed**: PowerShell 7 installed under `%ProgramFiles%\WindowsApps` is not yet part of the probe chain.
- **No prompt when every editor fails**: the chain ends at Notepad instead of asking you to pick one (you can still browse for an editor manually in the main window).
- **System theme changes are not watched live**: `Auto` is resolved at startup (or when you change the setting), so a mid-session theme change requires a restart or re-selecting the theme.
- Remaining design items that are not implemented yet (registry write mutex, a "re-run with the other version" entry after a fallback, etc.) are tracked as D16–D21 in [wiki/04-progress.md](wiki/04-progress.md).

## Documentation

| Document | Contents |
|---|---|
| [wiki/index.md](wiki/index.md) | Documentation overview and current status (start here) |
| [wiki/01-requirements.md](wiki/01-requirements.md) | Requirements R1–R9 and per-item acceptance criteria |
| [wiki/02-feature-matrix.md](wiki/02-feature-matrix.md) | 45 feature points × implementation site × status × acceptance notes (source of truth for status) |
| [wiki/03-project-structure.md](wiki/03-project-structure.md) | Directory plan, project split, build conventions |
| [wiki/04-progress.md](wiki/04-progress.md) | Milestones, completion, gap list (source of truth for progress) |
| [wiki/05-architecture-flows.md](wiki/05-architecture-flows.md) | Full call chain after a double-click, argument protocol, process lifetime |
| [wiki/06-tech-file-association.md](wiki/06-tech-file-association.md) | ProgId design, UserChoice hash wall, HKCU/HKLM permissions, uninstall strategy |
| [wiki/07-tech-powershell-encoding.md](wiki/07-tech-powershell-encoding.md) | PS5/PS7 probe order and encoding decision table |
| [wiki/08-tech-editor-discovery.md](wiki/08-tech-editor-discovery.md) | Editor discovery sources and fallback chain |
| [wiki/09-eval-dual-target-framework.md](wiki/09-eval-dual-target-framework.md) | Dual-target framework evaluation with measured data |
| [wiki/10-eval-background-residency.md](wiki/10-eval-background-residency.md) | Whether background residency is needed — the conclusion |
| [wiki/11-build-and-dev-guide.md](wiki/11-build-and-dev-guide.md) | Build, run, and troubleshooting guide |
| [wiki/12-appendix-registry-map.md](wiki/12-appendix-registry-map.md) | Registry snapshot of association-related keys on the test machine |

## Security Notes

- PS1Run only returns the double-click decision to you. It does **not** change the PowerShell execution policy and does not inspect script contents.
- Double-clicking an untrusted `.ps1` is still risky. The default "Ask every time" mode exists to give you a one-second confirmation before anything runs.
- Per-user association writes to `HKCU` and needs no elevation. Machine-level registration requests administrator rights — make sure the UAC prompt originates from this program.
- Logs contain script paths and launch arguments. Review them before sharing if they may include sensitive information.
