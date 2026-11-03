using System;

namespace RdpIt
{
    /// <summary>How a new session chooses its initial remote desktop resolution.</summary>
    public enum ResolutionMode
    {
        /// <summary>Start at the session window's client size and keep following it.</summary>
        FitToWindow = 0,

        /// <summary>Start at a fixed width/height (the window still auto-resizes after).</summary>
        Fixed = 1,

        /// <summary>Open the session full screen at the primary monitor's resolution.</summary>
        FullScreen = 2
    }

    /// <summary>
    /// Connection experience level, mirroring mstsc's "Performance" dropdown. Lower
    /// bandwidth levels disable visual features (wallpaper, themes, animations, font
    /// smoothing, desktop composition) to keep the session responsive.
    /// </summary>
    public enum ExperienceLevel
    {
        /// <summary>Modem (56 kbps): almost all visual features off.</summary>
        Modem = 0,

        /// <summary>Low-speed broadband (256 kbps - 2 Mbps).</summary>
        Broadband = 1,

        /// <summary>High-speed broadband / satellite (2 - 10 Mbps).</summary>
        HighSpeedBroadband = 2,

        /// <summary>LAN (10 Mbps or higher): all visual features on.</summary>
        Lan = 3,

        /// <summary>Let the control auto-detect the connection quality.</summary>
        AutoDetect = 4
    }

    /// <summary>
    /// Application-wide preferences (not per-connection). Serialized as plain JSON to
    /// %APPDATA%\RdpIt\settings.json. No secrets here, so no DPAPI needed.
    /// </summary>
    [Serializable]
    public sealed class AppSettings
    {
        /// <summary>Default resolution behaviour for new sessions.</summary>
        public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.FitToWindow;

        /// <summary>Default width used when <see cref="ResolutionMode"/> is Fixed.</summary>
        public int DefaultWidth { get; set; } = 1280;

        /// <summary>Default height used when <see cref="ResolutionMode"/> is Fixed.</summary>
        public int DefaultHeight { get; set; } = 720;

        /// <summary>
        /// When true, a resized session window continuously pushes the new resolution
        /// to the remote host. When false, the session keeps its initial resolution
        /// (and scales via smart-sizing instead). Applies to all modes.
        /// </summary>
        public bool AutoAdjustOnResize { get; set; } = true;

        /// <summary>Connection experience / performance level for new sessions.</summary>
        public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Lan;

        public AppSettings Clone()
        {
            return new AppSettings
            {
                ResolutionMode = ResolutionMode,
                DefaultWidth = DefaultWidth,
                DefaultHeight = DefaultHeight,
                AutoAdjustOnResize = AutoAdjustOnResize,
                ExperienceLevel = ExperienceLevel
            };
        }

        /// <summary>Clamp values into sane ranges so a bad config can't break sessions.</summary>
        public void Normalize()
        {
            if (DefaultWidth < 640) DefaultWidth = 640;
            if (DefaultWidth > 7680) DefaultWidth = 7680;
            if (DefaultHeight < 480) DefaultHeight = 480;
            if (DefaultHeight > 4320) DefaultHeight = 4320;
        }
    }
}
