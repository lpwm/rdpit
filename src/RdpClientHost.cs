using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RdpIt
{
    /// <summary>
    /// Hosts the Microsoft RDP Client ActiveX control (mstscax.dll) without requiring
    /// SDK-generated interop assemblies (AxImp/TlbImp). The control is instantiated by
    /// its CLSID through the <see cref="AxHost"/> base class, and all members are invoked
    /// late-bound via <c>dynamic</c> against the live COM object.
    ///
    /// We target "Microsoft RDP Client Control - version 10"
    /// CLSID {8B918B82-7985-4C24-89DF-C33AD2BBFBCD}, whose IMsRdpClient10 interface
    /// exposes UpdateSessionDisplaySettings for dynamic (smart) resolution changes.
    ///
    /// Connection state transitions are detected by polling the control's late-bound
    /// <c>Connected</c> property (0 = disconnected, 1 = connected, 2 = connecting).
    /// This avoids a COM connection-point event sink, which proved unreliable to wire
    /// without a generated typed interop assembly. On disconnect we read
    /// <c>ExtendedDisconnectReason</c> / the control's disconnect reason for a message.
    /// </summary>
    public sealed class RdpClientHost : AxHost
    {
        private const string RdpClient10Clsid = "8B918B82-7985-4C24-89DF-C33AD2BBFBCD";

        private dynamic _ocx;
        private readonly Timer _poll;
        private int _lastState = -1;       // -1 unknown, 0 disc, 1 conn, 2 connecting
        private bool _connecting;
        private bool _everConnected;
        private int _lastRequestedWidth;
        private int _lastRequestedHeight;
        private uint _lastRequestedScale = 100;

        public event EventHandler Connected;
        public event EventHandler<RdpDisconnectEventArgs> Disconnected;

        public RdpClientHost()
            : base(RdpClient10Clsid)
        {
            _poll = new Timer { Interval = 250 };
            _poll.Tick += (s, e) => PollState();
        }

        public dynamic Ocx => _ocx;

        /// <summary>Current connection state: 0 disconnected, 1 connected, 2 connecting.</summary>
        public int ConnectionState
        {
            get
            {
                try { return _ocx != null ? (int)_ocx.Connected : 0; }
                catch { return 0; }
            }
        }

        public bool IsConnected => ConnectionState == 1;

        protected override void AttachInterfaces()
        {
            _ocx = GetOcx();
        }

        public void Configure(string server, int port, string username, string password, string domain,
            ExperienceLevel experience = ExperienceLevel.Lan)
        {
            EnsureOcx();

            _ocx.Server = server;
            _ocx.UserName = username ?? string.Empty;
            if (!string.IsNullOrEmpty(domain))
                _ocx.Domain = domain;

            dynamic adv = _ocx.AdvancedSettings9;
            if (port > 0 && port != 3389)
                adv.RDPPort = port;

            if (!string.IsNullOrEmpty(password))
                adv.ClearTextPassword = password;   // enables non-interactive logon

            adv.EnableCredSspSupport = true;        // NLA
            adv.AuthenticationLevel = 0;            // connect even if server auth can't be verified
            adv.RedirectClipboard = true;
            adv.SmartSizing = false;                // we push true dynamic resolution instead
            adv.DisplayConnectionBar = true;
            adv.ConnectionBarShowMinimizeButton = true;
            adv.ConnectionBarShowRestoreButton = true;

            ApplyExperience(adv, experience);

            // NOTE: the caller sets the precise, DPI-correct desktop size via
            // SetDesktopSize(...) immediately after Configure, so we deliberately do
            // not negotiate a (stale, logical-pixel) size here.
        }

        // RDP performance flags (TS_PERF_*). We OR together the features to DISABLE.
        private const int PerfDisableWallpaper        = 0x00000001;
        private const int PerfDisableFullWindowDrag   = 0x00000002;
        private const int PerfDisableMenuAnimations   = 0x00000004;
        private const int PerfDisableThemes           = 0x00000008;
        private const int PerfDisableCursorShadow     = 0x00000020;
        private const int PerfDisableCursorSettings   = 0x00000040;
        private const int PerfEnableFontSmoothing     = 0x00000080;
        private const int PerfEnableDesktopComposition= 0x00000100;

        /// <summary>
        /// Translate an <see cref="ExperienceLevel"/> into the control's
        /// NetworkConnectionType and PerformanceFlags, mirroring mstsc's behaviour.
        /// </summary>
        private void ApplyExperience(dynamic adv, ExperienceLevel level)
        {
            // NetworkConnectionType values per IMsRdpClientAdvancedSettings:
            // 1=Modem, 2=LowBroadband, 3=Satellite, 4=HighBroadband, 5=WAN, 6=LAN, 7=AutoDetect.
            int netType;
            int perf; // bits set = features turned OFF (except the two "enable" bits)

            switch (level)
            {
                case ExperienceLevel.Modem:
                    netType = 1;
                    perf = PerfDisableWallpaper | PerfDisableFullWindowDrag
                         | PerfDisableMenuAnimations | PerfDisableThemes
                         | PerfDisableCursorShadow | PerfDisableCursorSettings;
                    break;

                case ExperienceLevel.Broadband:
                    netType = 2;
                    perf = PerfDisableWallpaper | PerfDisableFullWindowDrag
                         | PerfDisableMenuAnimations;
                    break;

                case ExperienceLevel.HighSpeedBroadband:
                    netType = 4;
                    perf = PerfDisableWallpaper;
                    break;

                case ExperienceLevel.AutoDetect:
                    netType = 7;
                    perf = 0; // let the server/control decide; enable niceties
                    perf |= PerfEnableFontSmoothing | PerfEnableDesktopComposition;
                    break;

                case ExperienceLevel.Lan:
                default:
                    netType = 6;
                    // Everything on: no disable bits, plus font smoothing + composition.
                    perf = PerfEnableFontSmoothing | PerfEnableDesktopComposition;
                    break;
            }

            try { adv.NetworkConnectionType = (uint)netType; } catch { }
            try { adv.PerformanceFlags = perf; } catch { }
            // Auto-detect quality is a separate flag on newer controls.
            try { adv.bandwidthDetection = (level == ExperienceLevel.AutoDetect); } catch { }
        }

        /// <summary>
        /// Set the initial remote desktop size (in physical pixels) and DPI scale that
        /// the session will negotiate at connect time. Setting the scale factor up front
        /// means the server renders at the correct DPI from the first frame, avoiding the
        /// brief "blurry then sharp" flip caused by a post-connect resolution change.
        /// </summary>
        public void SetDesktopSize(int width, int height, uint dpiScale = 100)
        {
            EnsureOcx();
            width = Math.Max(width, 200);
            height = Math.Max(height, 200);

            _lastRequestedWidth = width;
            _lastRequestedHeight = height;
            _lastRequestedScale = dpiScale;

            try
            {
                _ocx.DesktopWidth = width;
                _ocx.DesktopHeight = height;
            }
            catch { }

            // Tell the control the DPI up front so the initial negotiation is correct.
            try { _ocx.DesktopScaleFactor = dpiScale; } catch { }
            try { _ocx.DeviceScaleFactor = 100u; } catch { }
        }

        /// <summary>Last desktop size/scale we requested, to detect redundant updates.</summary>
        public int LastRequestedWidth => _lastRequestedWidth;
        public int LastRequestedHeight => _lastRequestedHeight;
        public uint LastRequestedScale => _lastRequestedScale;

        /// <summary>True if the given size/scale already matches what we last requested.</summary>
        public bool MatchesRequested(int width, int height, uint dpiScale) =>
            width == _lastRequestedWidth && height == _lastRequestedHeight
            && dpiScale == _lastRequestedScale;

        public void Connect()
        {
            EnsureOcx();
            if (IsConnected || _connecting) return;
            _connecting = true;
            _lastState = ConnectionState;
            _ocx.Connect();
            _poll.Start();
        }

        public void Disconnect()
        {
            try
            {
                if (_ocx != null && IsConnected)
                    _ocx.Disconnect();
            }
            catch { }
        }

        /// <summary>
        /// Change the remote desktop resolution to match a new client size without
        /// reconnecting, via IMsRdpClient10.UpdateSessionDisplaySettings. Redundant
        /// updates (same size + scale already in effect) are skipped to avoid an
        /// unnecessary full-screen refresh.
        /// </summary>
        public void UpdateResolution(int width, int height, uint dpiScale = 100)
        {
            UpdateResolution(width, height, dpiScale, force: false);
        }

        /// <summary>
        /// Force the session to re-negotiate its display settings even if the size and
        /// scale are unchanged. Used right after connect to make the server re-render
        /// the desktop crisply at the exact client resolution.
        /// </summary>
        public void ForceUpdateResolution(int width, int height, uint dpiScale = 100)
        {
            UpdateResolution(width, height, dpiScale, force: true);
        }

        private void UpdateResolution(int width, int height, uint dpiScale, bool force)
        {
            if (!IsConnected) return;
            width = Math.Max(width, 200);
            height = Math.Max(height, 200);

            // Skip redundant updates unless forced: pushing the same size/scale the
            // session already runs at causes an unnecessary full-screen refresh.
            if (!force && MatchesRequested(width, height, dpiScale)) return;

            _lastRequestedWidth = width;
            _lastRequestedHeight = height;
            _lastRequestedScale = dpiScale;

            try
            {
                _ocx.UpdateSessionDisplaySettings(
                    (uint)width, (uint)height,
                    (uint)width, (uint)height,
                    0u,          // orientation: landscape
                    dpiScale,    // desktopScaleFactor
                    100u);       // deviceScaleFactor
            }
            catch (COMException)
            {
                try { _ocx.AdvancedSettings9.SmartSizing = true; } catch { }
            }
            catch { }
        }

        /// <summary>Poll the control's Connected property and raise transitions.</summary>
        private void PollState()
        {
            int state = ConnectionState;
            if (state == _lastState) return;

            int prev = _lastState;
            _lastState = state;

            if (state == 1)
            {
                _connecting = false;
                _everConnected = true;
                Connected?.Invoke(this, EventArgs.Empty);
            }
            else if (state == 0 && prev != 0)
            {
                // Transitioned to disconnected (from connected or connecting).
                _connecting = false;
                _poll.Stop();
                Disconnected?.Invoke(this, new RdpDisconnectEventArgs(ReadDisconnectReason()));
            }
        }

        private int ReadDisconnectReason()
        {
            // Prefer the extended reason; fall back to the basic disconnect reason.
            try { return (int)_ocx.ExtendedDisconnectReason; } catch { }
            try { return (int)_ocx.DisconnectedReason; } catch { }
            return _everConnected ? 0 : 516; // generic "unable to connect" if never up
        }

        private void EnsureOcx()
        {
            if (_ocx == null)
            {
                if (!IsHandleCreated)
                    CreateControl();
                if (_ocx == null)
                    _ocx = GetOcx();
            }
            if (_ocx == null)
                throw new InvalidOperationException("RDP ActiveX control is not available yet.");
        }
    }

    public sealed class RdpDisconnectEventArgs : EventArgs
    {
        public int Reason { get; }
        public RdpDisconnectEventArgs(int reason) { Reason = reason; }

        public string Describe()
        {
            switch (Reason)
            {
                case 0: return "Disconnected.";
                case 1: return "Remote disconnect by user.";
                case 2: return "Remote disconnect by server.";
                case 3: return "Idle timeout.";
                case 260: return "DNS name lookup failed - check the host name.";
                case 264: return "Connection timed out - host unreachable or RDP not enabled.";
                case 516: return "Unable to connect - the server refused or RDP is disabled.";
                case 772: return "Network error during the session.";
                case 2308: return "Socket closed - connection lost.";
                case 2825: return "The remote computer requires authentication (NLA) that failed.";
                case 3591: return "Credentials were rejected (bad username or password).";
                default: return "Disconnected (reason code " + Reason + ").";
            }
        }
    }
}
