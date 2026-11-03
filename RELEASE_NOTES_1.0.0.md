# RdpIt 1.0.0

First public release of **RdpIt** — a lightweight Windows Remote Desktop client
that wraps the built-in Microsoft RDP ActiveX control (`mstscax.dll`) in a small,
focused WinForms UI.

## Highlights

- **Connection manager** — save, edit, duplicate, and delete multiple RDP profiles
  (host/IP, port, username, optional domain, password).
- **One-click connect** — double-click or select a saved connection to connect
  instantly with the stored credentials; no prompts.
- **Secure credential storage** — passwords are encrypted at rest with Windows
  DPAPI (per-user), stored in `%APPDATA%\RdpIt\connections.json`.
- **Auto-adjusting resolution** — the remote desktop resolution follows the window
  as you resize it (true dynamic resolution via `UpdateSessionDisplaySettings`, not
  image scaling).
- **Configurable display modes** — Fit-to-window, Fixed resolution (presets from
  1920×1080 down to 800×600, or a custom size), or Full screen.
- **Connection experience levels** — choose a performance profile (Modem,
  Low-speed broadband, High-speed broadband, LAN, or auto-detect) that toggles
  visual features like wallpaper, themes, animations, and font smoothing.
- **Per-connection window memory** — in Fit-to-window mode each connection
  remembers the size you last used and reopens at that size.
- **Desktop shortcuts** — right-click a connection → *Create shortcut* to drop a
  `.lnk` on your desktop that connects directly to that profile.
- **Custom application icon** across the app window, taskbar, and shortcuts.
- **Version shown in the status bar** (bottom-right corner).

## Features in detail

### Connections
- Add / edit / delete saved profiles via the toolbar or the right-click menu.
- Fields: display name, host/IP, port (default 3389), username, domain, password.
- Clipboard redirection and Network Level Authentication (NLA) are enabled.

### Display & resolution
- **Fit to window** (default): the session starts at the window size and the remote
  resolution updates automatically when you resize (debounced while dragging).
- **Fixed resolution**: the session opens at an exact resolution; the window is
  sized so the remote desktop gets precisely that resolution (status bar excluded).
- **Full screen**: borderless at the primary monitor's resolution.
- **Auto-adjust on resize** can be turned off to keep a session at its initial
  resolution.
- A forced display re-negotiation right after connect ensures the first frame is
  rendered crisply at the correct resolution.

### Experience / performance
- Performance level mirrors mstsc's quality dropdown and maps to the control's
  `NetworkConnectionType` and `PerformanceFlags`.

### Shortcuts & direct connect
- `RdpIt.exe --connect <connectionId>` launches straight into a saved session,
  skipping the manager window — this is what desktop shortcuts invoke.
- A missing/renamed connection shows a friendly message instead of failing.

## Requirements

- Windows 10 / 11 (x64).
- .NET Framework 4.8 runtime (included with Windows 10/11).
- The Microsoft RDP ActiveX control (`mstscax.dll`), present on Windows by default.

## Install & run

Download `RdpIt.exe` from the release assets and run it — no installer required.
Settings and saved connections are created under `%APPDATA%\RdpIt\`.

To build from source:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\bin\RdpIt.exe
```

The build fetches a standalone Roslyn compiler (~21 MB) into `.tools\` on first
run, then compiles directly against the .NET Framework 4.8 runtime assemblies — no
Visual Studio, .NET SDK, or Targeting Pack required.

## Data & privacy

- Connection passwords are DPAPI-encrypted (CurrentUser scope) and only decryptable
  by the same Windows account on the same machine.
- App settings (`%APPDATA%\RdpIt\settings.json`) contain no secrets.

## Known limitations

- Single active session per window; no connection tabs yet.
- `AuthenticationLevel` is relaxed so the client connects even when the server's
  identity can't be verified (convenient for lab/self-signed hosts). Strict server
  authentication is not yet configurable from the UI.
- x64 only.

---

**Full source:** https://github.com/lpwm/rdpit
