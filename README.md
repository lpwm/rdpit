# RdpIt — a minimal Windows RDP client

A small WinForms Remote Desktop client that wraps the built-in Microsoft RDP
ActiveX control (`mstscax.dll`). It provides:

1. **Auto-adjusting remote resolution** — when you resize the session window, the
   remote desktop resolution follows automatically (true dynamic resolution via
   `IMsRdpClient10.UpdateSessionDisplaySettings`, not just image scaling).
2. **Saved connections** — add/edit/delete multiple profiles (host, port, username,
   password, optional domain). Passwords are encrypted at rest with Windows DPAPI
   (per-user) in `%APPDATA%\RdpIt\connections.json`.
3. **One-click connect** — selecting a saved connection connects immediately using
   the stored credentials, with no prompts.
4. **Settings panel** — customize new-session defaults:
   - Resolution mode:
     - **Fit to window** — start at the window size and follow it on resize (default).
     - **Fixed resolution** — pick a preset (1920×1080 … 800×600) or enter custom
       width/height.
     - **Full screen** — open borderless at the primary monitor resolution.
   - **Auto-adjust on resize** toggle — when off, the session keeps its initial
     resolution instead of re-resolutioning as you drag.
   - **Connection experience level** — mirrors mstsc's performance dropdown
     (Modem / Low-speed broadband / High-speed broadband / LAN / auto-detect).
     Lower levels disable visual features (wallpaper, themes, animations, font
     smoothing, desktop composition) via the control's `PerformanceFlags` and
     `NetworkConnectionType`.

   Settings are stored as plain JSON in `%APPDATA%\RdpIt\settings.json` (no secrets,
   so no DPAPI). Open the panel with the **Settings** button on the main window.
5. **Create desktop shortcut** — right-click a saved connection → **Create shortcut**
   to drop a `.lnk` on your desktop that launches `RdpIt.exe --connect <id>` and
   connects directly to that profile (no manager UI). Shortcuts are created via the
   Windows Script Host (`WScript.Shell`) through late binding, so no extra references
   are needed. The shortcut uses the app's embedded icon.
6. **Remembers window size per connection** — in Fit-to-window mode, each session
   saves its window size on close onto that specific connection, and the next time
   you open that connection it reopens at the same size (and therefore the same
   remote resolution). Stored as `LastWindowWidth`/`LastWindowHeight` on each entry
   in `connections.json`, clamped to the current screen on reopen. Fixed and
   Full-screen modes are explicit choices and are not overridden by this.
7. **Application icon** — a custom icon (`assets\rdpit.ico`, multi-resolution
   16–256 px) is embedded in the executable (`csc /win32icon`) and applied to every
   window, the taskbar, and desktop shortcuts.

## Why it's built this way

The machine has **no Visual Studio, no .NET SDK, and no .NET Framework Targeting
Pack** — only the .NET Framework 4.8 runtime and its legacy `csc.exe` (C# 5 only).
To build a modern WinForms app here:

- A standalone **Roslyn compiler** is fetched as a NuGet package into `.tools\roslyn`
  (no install, no admin) — see `build.ps1`.
- The build compiles directly with `csc.exe`, referencing the **runtime** assemblies
  (since there is no Targeting Pack).
- The RDP ActiveX control is hosted via `System.Windows.Forms.AxHost` by **CLSID**
  (`{8B918B82-7985-4C24-89DF-C33AD2BBFBCD}`, "RDP Client Control v10"), and its
  members are called late-bound through `dynamic` — so **no AxImp/TlbImp-generated
  interop assemblies are needed**.
- Connect/disconnect are detected by **polling** the control's `Connected` property
  (a COM connection-point event sink proved unreliable to wire without a typed
  interop assembly).

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `bin\RdpIt.exe` (x64, .NET Framework 4.8).

The first build step downloads the Roslyn compiler package (~21 MB) into `.tools\`.
Subsequent builds reuse it and need no network access.

## Run

```powershell
.\bin\RdpIt.exe
```

- **New** — create a connection (host/IP, port, username, domain, password).
- **Connect** or double-click — opens a session window using saved credentials.
- **Right-click a connection** — Connect / Edit / **Create shortcut** / Delete.
- Resize the session window — the remote resolution updates when you stop dragging.

### Direct-connect (used by desktop shortcuts)

```powershell
.\bin\RdpIt.exe --connect <connectionId>
```

Opens straight into a session for the saved connection with that Id, skipping the
manager window. This is what the "Create shortcut" desktop `.lnk` files invoke.

### Self-test

```powershell
.\bin\RdpIt.exe --selftest
```

Attempts a connection to `127.0.0.1:3390` (no listener) and writes
`bin\selftest.txt` confirming the disconnect-detection pipeline works.

## Project layout

```
RdpIt.csproj             MSBuild project (reference; direct csc build is in build.ps1)
app.manifest             Per-monitor-v2 DPI awareness + Win10/11 compat
build.ps1                Builds with the Roslyn compiler against runtime assemblies
make-icon.ps1            Regenerates assets\rdpit.ico (pure GDI+, no external tools)
assets/
  rdpit.ico              Multi-resolution app icon (embedded into the exe)
src/
  Program.cs             Entry point (+ --selftest and --connect <id> modes)
  RdpClientHost.cs       AxHost wrapper for the RDP ActiveX control + state polling
  Connection.cs          Connection profile model
  ConnectionStore.cs     DPAPI-encrypted JSON persistence for connections
  AppSettings.cs         App-wide preferences (resolution, experience, last size)
  SettingsStore.cs       Plain-JSON persistence for AppSettings
  ShortcutService.cs     Creates desktop .lnk shortcuts (WScript.Shell, late-bound)
  AppIcon.cs             Loads the embedded/asset icon and applies it to windows
  MainForm.cs            Connection manager UI + right-click context menu
  ConnectionEditForm.cs  Add/edit connection dialog
  SettingsForm.cs        Settings panel (resolution + experience level)
  SessionForm.cs         Live session window; dynamic resolution + size memory
```

## Security notes

- Passwords are protected with DPAPI (`DataProtectionScope.CurrentUser`) plus a
  fixed entropy value, so the stored blob is only decryptable by the same Windows
  user on the same machine.
- `AuthenticationLevel = 0` means the client will connect even if the server's
  identity can't be verified (useful for lab/self-signed hosts). Raise this if you
  require strict server authentication.
```
