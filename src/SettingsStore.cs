using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace RdpIt
{
    /// <summary>
    /// Loads/saves <see cref="AppSettings"/> to %APPDATA%\RdpIt\settings.json.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string _path;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public AppSettings Settings { get; private set; } = new AppSettings();

        public SettingsStore()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RdpIt");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
        }

        public string FilePath => _path;

        public void Load()
        {
            Settings = new AppSettings();
            if (!File.Exists(_path)) return;
            try
            {
                var text = File.ReadAllText(_path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return;
                var loaded = _json.Deserialize<AppSettings>(text);
                if (loaded != null)
                {
                    loaded.Normalize();
                    Settings = loaded;
                }
            }
            catch
            {
                // Corrupt file: fall back to defaults rather than crash.
                Settings = new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            settings.Normalize();
            Settings = settings;
            var text = _json.Serialize(settings);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
    }
}
