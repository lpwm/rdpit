using System;

namespace RdpIt
{
    /// <summary>
    /// A saved remote desktop connection profile. The password is stored on disk as
    /// a DPAPI-encrypted, Base64-encoded blob (see <see cref="ConnectionStore"/>),
    /// never in plain text.
    /// </summary>
    [Serializable]
    public sealed class Connection
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Domain { get; set; }

        /// <summary>DPAPI-encrypted password, Base64 encoded. Not the plain text.</summary>
        public string EncryptedPassword { get; set; }

        /// <summary>
        /// Width of the session window (client area) the last time this connection was
        /// used in Fit-to-window mode, so it reopens at the same size. 0 = not recorded.
        /// </summary>
        public int LastWindowWidth { get; set; }

        /// <summary>Height of the last session window for this connection. 0 = none.</summary>
        public int LastWindowHeight { get; set; }

        /// <summary>True once this connection has a remembered window size to reuse.</summary>
        public bool HasLastWindowSize => LastWindowWidth > 0 && LastWindowHeight > 0;

        public Connection()
        {
            Id = Guid.NewGuid().ToString("N");
            Port = 3389;
        }

        public string DisplayName
        {
            get
            {
                var label = string.IsNullOrWhiteSpace(Name) ? Host : Name;
                var user = string.IsNullOrWhiteSpace(Username) ? "" : "  (" + Username + ")";
                return label + user;
            }
        }
    }
}
