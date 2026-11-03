using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace RdpIt
{
    /// <summary>
    /// Loads and saves connection profiles to %APPDATA%\RdpIt\connections.json.
    /// Passwords are protected per-user with Windows DPAPI
    /// (<see cref="ProtectedData"/>, CurrentUser scope), so the file is only usable
    /// by the Windows account that created it.
    /// </summary>
    public sealed class ConnectionStore
    {
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("RdpIt::v1::credential-entropy");

        private readonly string _path;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public List<Connection> Connections { get; private set; } = new List<Connection>();

        public ConnectionStore()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RdpIt");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "connections.json");
        }

        public string FilePath => _path;

        public void Load()
        {
            Connections = new List<Connection>();
            if (!File.Exists(_path)) return;
            try
            {
                var text = File.ReadAllText(_path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return;
                var list = _json.Deserialize<List<Connection>>(text);
                if (list != null) Connections = list;
            }
            catch
            {
                // Corrupt file: start fresh rather than crash. (Kept on disk for recovery.)
                Connections = new List<Connection>();
            }
        }

        public void Save()
        {
            var text = _json.Serialize(Connections);
            // Write atomically via a temp file + replace to avoid truncation on crash.
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }

        public void Upsert(Connection c)
        {
            var idx = Connections.FindIndex(x => x.Id == c.Id);
            if (idx >= 0) Connections[idx] = c;
            else Connections.Add(c);
        }

        public void Remove(string id)
        {
            Connections.RemoveAll(x => x.Id == id);
        }

        // ---- DPAPI credential helpers ----

        /// <summary>Encrypt a plain-text password for storage.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        /// <summary>Decrypt a stored password back to plain text (used only at connect time).</summary>
        public static string Unprotect(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;
            try
            {
                var enc = Convert.FromBase64String(encryptedBase64);
                var dec = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch
            {
                return string.Empty; // wrong user / corrupted blob
            }
        }
    }
}
