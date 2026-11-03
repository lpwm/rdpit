using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace RdpIt
{
    /// <summary>
    /// Provides the application icon to all windows. The icon is embedded in the
    /// executable at build time (csc /win32icon) and also shipped as assets\rdpit.ico;
    /// this loads whichever is available, cached as a single shared instance.
    /// </summary>
    public static class AppIcon
    {
        private static Icon _icon;
        private static bool _tried;

        public static Icon Get()
        {
            if (_tried) return _icon;
            _tried = true;

            // 1) The icon embedded in RdpIt.exe itself.
            try
            {
                var exe = typeof(AppIcon).Assembly.Location;
                if (!string.IsNullOrEmpty(exe) && System.IO.File.Exists(exe))
                {
                    var extracted = Icon.ExtractAssociatedIcon(exe);
                    if (extracted != null) { _icon = extracted; return _icon; }
                }
            }
            catch { }

            // 2) assets\rdpit.ico next to the exe (dev / fallback).
            try
            {
                var dir = System.IO.Path.GetDirectoryName(typeof(AppIcon).Assembly.Location) ?? ".";
                foreach (var candidate in new[]
                {
                    System.IO.Path.Combine(dir, "rdpit.ico"),
                    System.IO.Path.Combine(dir, "assets", "rdpit.ico"),
                    System.IO.Path.Combine(dir, "..", "assets", "rdpit.ico"),
                })
                {
                    if (System.IO.File.Exists(candidate))
                    {
                        _icon = new Icon(candidate);
                        return _icon;
                    }
                }
            }
            catch { }

            return _icon; // may be null; forms simply keep the default icon then
        }

        /// <summary>Apply the app icon to a form if one is available.</summary>
        public static void Apply(Form form)
        {
            var icon = Get();
            if (icon != null) form.Icon = icon;
        }
    }
}
