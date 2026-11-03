using System;
using System.IO;
using System.Reflection;

namespace RdpIt
{
    /// <summary>
    /// Creates Windows .lnk shortcuts on the Desktop that launch RdpIt directly into
    /// a saved connection (via "RdpIt.exe --connect &lt;id&gt;").
    ///
    /// Uses the Windows Script Host "WScript.Shell" COM object through late binding,
    /// so no COM interop reference (and no SDK) is required.
    /// </summary>
    public static class ShortcutService
    {
        /// <summary>Full path to the RdpIt executable (the assembly that defines this type).</summary>
        public static string ExecutablePath
        {
            get
            {
                // typeof(...).Assembly is always RdpIt.exe, even when the assembly is
                // loaded for reflection by another host (e.g. a test runner). Prefer it
                // over GetEntryAssembly()/the current process, which can resolve to the
                // host executable instead.
                var loc = typeof(ShortcutService).Assembly.Location;
                if (!string.IsNullOrEmpty(loc)) return loc;

                var entry = Assembly.GetEntryAssembly();
                if (entry != null && !string.IsNullOrEmpty(entry.Location)) return entry.Location;

                return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            }
        }

        /// <summary>
        /// Create (or overwrite) a desktop shortcut for the given connection.
        /// Returns the full path of the created .lnk file.
        /// </summary>
        public static string CreateDesktopShortcut(Connection conn)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var label = string.IsNullOrWhiteSpace(conn.Name) ? conn.Host : conn.Name;
            var fileName = SanitizeFileName(label) + ".lnk";
            var linkPath = Path.Combine(desktop, fileName);

            var exe = ExecutablePath;

            // Late-bound WScript.Shell so we don't need the IWshRuntimeLibrary reference.
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("Windows Script Host is not available on this system.");

            object shell = Activator.CreateInstance(shellType);
            try
            {
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell,
                    new object[] { linkPath });

                var sType = shortcut.GetType();
                SetProp(sType, shortcut, "TargetPath", exe);
                SetProp(sType, shortcut, "Arguments", "--connect " + conn.Id);
                SetProp(sType, shortcut, "WorkingDirectory", Path.GetDirectoryName(exe) ?? "");
                SetProp(sType, shortcut, "Description", "Connect to " + label + " via RdpIt");
                SetProp(sType, shortcut, "IconLocation", exe + ",0");

                sType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }

            return linkPath;
        }

        private static void SetProp(Type t, object target, string name, object value)
        {
            t.InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "RDP Connection";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            return name.Length == 0 ? "RDP Connection" : name;
        }
    }
}
