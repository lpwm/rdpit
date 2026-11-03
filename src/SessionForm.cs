using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpIt
{
    /// <summary>
    /// A window hosting one live RDP session. The remote desktop resolution is
    /// automatically updated to match the window's client area whenever the user
    /// resizes the window (debounced so we only push a new resolution once the
    /// user stops dragging).
    /// </summary>
    public sealed class SessionForm : Form
    {
        private readonly Connection _conn;
        private readonly AppSettings _settings;
        private readonly SettingsStore _settingsStore;
        private readonly ConnectionStore _connectionStore;
        private readonly RdpClientHost _rdp;
        private readonly Timer _resizeDebounce;
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _statusLabel = new ToolStripStatusLabel();
        private bool _started;
        private bool _sizeSaved;

        public SessionForm(Connection conn, AppSettings settings)
            : this(conn, settings, null, null)
        {
        }

        public SessionForm(Connection conn, AppSettings settings, SettingsStore settingsStore)
            : this(conn, settings, settingsStore, null)
        {
        }

        /// <param name="settingsStore">App-wide settings store (unused for size now, kept for parity).</param>
        /// <param name="connectionStore">
        /// Store used to persist this connection's last window size. When null, the
        /// session still runs but won't remember its size.
        /// </param>
        public SessionForm(Connection conn, AppSettings settings,
            SettingsStore settingsStore, ConnectionStore connectionStore)
        {
            _conn = conn;
            _settings = settings ?? new AppSettings();
            _settingsStore = settingsStore;
            _connectionStore = connectionStore;

            Text = "RDP — " + (string.IsNullOrWhiteSpace(conn.Name) ? conn.Host : conn.Name);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(640, 480);
            Font = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            _status.Items.Add(_statusLabel);
            _statusLabel.Text = "Connecting to " + conn.Host + " ...";

            _rdp = new RdpClientHost { Dock = DockStyle.Fill };
            _rdp.Connected += OnConnected;
            _rdp.Disconnected += OnDisconnected;

            // Add controls BEFORE sizing so the status strip's height is known and can
            // be accounted for when we size the window to a fixed RDP resolution.
            Controls.Add(_rdp);
            Controls.Add(_status);

            ApplyInitialWindowSize();

            _resizeDebounce = new Timer { Interval = 350 };
            _resizeDebounce.Tick += (s, e) =>
            {
                _resizeDebounce.Stop();
                ApplyResolution();
            };

            Load += OnLoad;
            ResizeEnd += (s, e) => QueueResolutionUpdate();
            SizeChanged += (s, e) =>
            {
                // SizeChanged fires continuously while dragging and also for
                // maximize/restore (which don't raise ResizeEnd); debounce covers both.
                if (_started && _settings.AutoAdjustOnResize
                    && WindowState != FormWindowState.Minimized)
                    QueueResolutionUpdate();
            };
            FormClosing += (s, e) =>
            {
                SaveWindowSize();
                _rdp.Disconnect();
            };
        }

        /// <summary>
        /// Height (logical px) of the chrome that sits below the RDP surface inside the
        /// form's client area — currently just the status strip. The window client size
        /// must include this so the RDP control itself gets the requested resolution.
        /// Uses the strip's own Height (valid even before the form is shown) rather than
        /// its Visible flag, which is false until the form is displayed.
        /// </summary>
        private int NonSurfaceHeight => _status != null ? _status.Height : 0;

        /// <summary>Size/position the window according to the configured default mode.</summary>
        private void ApplyInitialWindowSize()
        {
            switch (_settings.ResolutionMode)
            {
                case ResolutionMode.Fixed:
                    // Size the window so the RDP control (not the whole client area)
                    // gets exactly DefaultWidth x DefaultHeight. The status strip height
                    // is added on top so it doesn't eat into the remote resolution.
                    ClientSize = new Size(
                        _settings.DefaultWidth,
                        _settings.DefaultHeight + NonSurfaceHeight);
                    break;

                case ResolutionMode.FullScreen:
                    // Borderless, covering the primary screen; the remote desktop size
                    // is set to the full screen resolution at connect time.
                    var screen = Screen.PrimaryScreen.Bounds;
                    FormBorderStyle = FormBorderStyle.None;
                    WindowState = FormWindowState.Maximized;
                    ClientSize = new Size(screen.Width, screen.Height);
                    break;

                default: // FitToWindow
                    // Reopen at the size this specific connection last used, if known;
                    // otherwise fall back to a sensible default.
                    ClientSize = _conn.HasLastWindowSize
                        ? ClampToScreen(_conn.LastWindowWidth, _conn.LastWindowHeight)
                        : new Size(1024, 768);
                    break;
            }
        }

        /// <summary>Keep a remembered size from exceeding the current working area.</summary>
        private static Size ClampToScreen(int w, int h)
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            if (w > wa.Width) w = wa.Width;
            if (h > wa.Height) h = wa.Height;
            if (w < 640) w = 640;
            if (h < 480) h = 480;
            return new Size(w, h);
        }

        /// <summary>The initial remote desktop size (physical pixels) for the mode.</summary>
        private Size InitialDesktopSize() => TargetDesktopSize();

        /// <summary>
        /// Persist the current window's client size onto THIS connection so the next
        /// time it is opened (in Fit-to-window mode) it reuses the same size, and thus
        /// the same remote resolution. Fixed/FullScreen are explicit choices and not
        /// remembered.
        /// </summary>
        private void SaveWindowSize()
        {
            if (_sizeSaved) return;
            _sizeSaved = true;

            if (_connectionStore == null) return;
            if (_settings.ResolutionMode != ResolutionMode.FitToWindow) return;
            if (!_started) return; // never actually opened a session

            // Use the restore bounds when maximized so we remember a usable size.
            Size size = WindowState == FormWindowState.Normal
                ? ClientSize
                : RestoreBounds.Size;

            int w = Math.Max(size.Width, 640);
            int h = Math.Max(size.Height, 480);
            if (w < 200 || h < 200) return;

            try
            {
                // Update the stored connection (find by Id to avoid clobbering other
                // fields that may have changed elsewhere).
                var stored = _connectionStore.Connections.Find(c => c.Id == _conn.Id);
                if (stored != null)
                {
                    stored.LastWindowWidth = w;
                    stored.LastWindowHeight = h;
                }
                else
                {
                    _conn.LastWindowWidth = w;
                    _conn.LastWindowHeight = h;
                    _connectionStore.Upsert(_conn);
                }
                // Keep the in-memory instance in sync too.
                _conn.LastWindowWidth = w;
                _conn.LastWindowHeight = h;
                _connectionStore.Save();
            }
            catch { /* non-fatal: failing to remember a size shouldn't break teardown */ }
        }

        // The RDP desktop resolution must be expressed in PHYSICAL pixels, while the
        // control's ClientSize is in logical (DPI-scaled) units under PerMonitorV2
        // awareness. Convert using the current DPI so what we negotiate matches what
        // the control actually displays — eliminating the post-connect resize/refresh.
        private double DpiFactor
        {
            get
            {
                try { using (var g = CreateGraphics()) return g.DpiX / 96.0; }
                catch { return 1.0; }
            }
        }

        private int PhysicalSurfaceWidth =>
            Math.Max((int)Math.Round(_rdp.ClientSize.Width * DpiFactor), 200);
        private int PhysicalSurfaceHeight =>
            Math.Max((int)Math.Round(_rdp.ClientSize.Height * DpiFactor), 200);

        /// <summary>
        /// The remote desktop resolution to negotiate/report right now, in physical
        /// pixels. For Fixed/FullScreen this is the explicit configured value, so the
        /// session reports exactly what the user set (e.g. 1600x900). For FitToWindow
        /// it tracks the live RDP control surface.
        /// </summary>
        private Size TargetDesktopSize()
        {
            switch (_settings.ResolutionMode)
            {
                case ResolutionMode.Fixed:
                    return new Size(_settings.DefaultWidth, _settings.DefaultHeight);
                case ResolutionMode.FullScreen:
                    var b = Screen.PrimaryScreen.Bounds;
                    return new Size(b.Width, b.Height);
                default:
                    return new Size(PhysicalSurfaceWidth, PhysicalSurfaceHeight);
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            try
            {
                _rdp.CreateControl();   // force the ActiveX handle + interface attach

                // Ensure the control is laid out at its final size before we read it,
                // so the resolution we negotiate matches the real surface exactly.
                PerformLayout();
                _rdp.Update();

                _rdp.Configure(
                    _conn.Host,
                    _conn.Port,
                    _conn.Username,
                    ConnectionStore.Unprotect(_conn.EncryptedPassword),
                    _conn.Domain,
                    _settings.ExperienceLevel);

                var scale = GetDpiScalePercent();
                var initial = InitialDesktopSize();          // physical pixels
                _rdp.SetDesktopSize(initial.Width, initial.Height, scale);
                _rdp.Connect();
                _started = true;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Failed to start session: " + ex.Message;
                MessageBox.Show(this,
                    "Could not start the RDP session:\n\n" + ex.Message,
                    "Connection error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void QueueResolutionUpdate()
        {
            _resizeDebounce.Stop();
            _resizeDebounce.Start();
        }

        private void ApplyResolution()
        {
            if (!_rdp.IsConnected) return;
            var scale = GetDpiScalePercent();
            var target = TargetDesktopSize();
            _rdp.UpdateResolution(target.Width, target.Height, scale);  // no-ops if unchanged
            _statusLabel.Text = string.Format("Connected to {0} — {1}x{2} @ {3}%",
                _conn.Host, target.Width, target.Height, scale);
        }

        private uint GetDpiScalePercent()
        {
            try
            {
                using (var g = CreateGraphics())
                {
                    var pct = (uint)Math.Round(g.DpiX / 96.0 * 100.0);
                    // RDP accepts 100/125/150/175/200; clamp to the nearest allowed step.
                    uint[] allowed = { 100, 125, 150, 175, 200 };
                    uint best = 100; var bestDiff = uint.MaxValue;
                    foreach (var a in allowed)
                    {
                        var d = (uint)Math.Abs((int)a - (int)pct);
                        if (d < bestDiff) { bestDiff = d; best = a; }
                    }
                    return best;
                }
            }
            catch { return 100; }
        }

        private void OnConnected(object sender, EventArgs e)
        {
            var target = TargetDesktopSize();
            var scale = GetDpiScalePercent();

            _statusLabel.Text = string.Format("Connected to {0} — {1}x{2} @ {3}%",
                _conn.Host, target.Width, target.Height, scale);

            // Force a display-settings re-negotiation directly after the connection is
            // established. Even though we negotiated the exact size up front, some hosts
            // render the first frames at a stale size/scale; a forced update makes the
            // server re-render crisply at the real client resolution.
            _rdp.ForceUpdateResolution(target.Width, target.Height, scale);
        }

        private void OnDisconnected(object sender, RdpDisconnectEventArgs e)
        {
            _statusLabel.Text = e.Describe();
            // If it failed before we ever connected, surface the reason prominently.
            if (!_started || e.Reason >= 260)
            {
                MessageBox.Show(this, e.Describe(), "Disconnected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Close();
        }
    }
}
