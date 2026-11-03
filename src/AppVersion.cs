using System;
using System.Reflection;

namespace RdpIt
{
    /// <summary>Exposes the application's version for display in the UI.</summary>
    public static class AppVersion
    {
        /// <summary>Short version string like "1.0.0" (Major.Minor.Build).</summary>
        public static string Short
        {
            get
            {
                try
                {
                    // Prefer the informational version ("1.0.0") if present.
                    var asm = typeof(AppVersion).Assembly;
                    var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
                        return info.InformationalVersion.Trim();

                    var v = asm.GetName().Version ?? new Version(1, 0, 0, 0);
                    return v.Major + "." + v.Minor + "." + v.Build;
                }
                catch
                {
                    return "1.0.0";
                }
            }
        }

        /// <summary>Label for the status bar, e.g. "v1.0.0".</summary>
        public static string Label => "v" + Short;
    }
}
