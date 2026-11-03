using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpIt
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && args[0] == "--selftest")
            {
                RunSelfTest();
                return;
            }

            // Shortcut / direct-connect mode: "RdpIt.exe --connect <connectionId>".
            // Launches straight into a session for the saved connection, no manager UI.
            if (args != null && args.Length >= 2 && args[0] == "--connect")
            {
                RunDirectConnect(args[1]);
                return;
            }

            Application.Run(new MainForm());
        }

        /// <summary>
        /// Open a session for a single saved connection by Id and run until it closes.
        /// Used by desktop shortcuts created from the connection manager.
        /// </summary>
        private static void RunDirectConnect(string connectionId)
        {
            var store = new ConnectionStore();
            store.Load();
            var conn = store.Connections.Find(c => c.Id == connectionId);

            if (conn == null)
            {
                MessageBox.Show(
                    "This shortcut points to a connection that no longer exists.\n\n" +
                    "It may have been deleted or renamed. Open RdpIt to recreate the shortcut.",
                    "RdpIt - connection not found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var settingsStore = new SettingsStore();
            settingsStore.Load();

            Application.Run(new SessionForm(conn, settingsStore.Settings, settingsStore, store));
        }

        /// <summary>
        /// Headless-ish test: create the control, attempt to connect to a local port
        /// with no listener, and confirm the OnDisconnected event flows through our
        /// connection-point sink. Writes PASS/FAIL to stdout and exits.
        /// </summary>
        private static void RunSelfTest()
        {
            var form = new Form { Size = new Size(700, 500), Text = "RdpIt self-test" };
            var rdp = new RdpClientHost { Dock = DockStyle.Fill };
            form.Controls.Add(rdp);

            bool disconnected = false;
            int reason = -1;

            rdp.Disconnected += (s, e) =>
            {
                disconnected = true;
                reason = e.Reason;
                form.Close();
            };

            var timeout = new Timer { Interval = 30000 };
            timeout.Tick += (s, e) => { timeout.Stop(); form.Close(); };

            form.Shown += (s, e) =>
            {
                try
                {
                    rdp.CreateControl();
                    rdp.Configure("127.0.0.1", 3390, "tester", "", "");
                    rdp.Connect();                 // starts polling for state transitions
                    timeout.Start();
                }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"),
                        "SELFTEST ERROR: " + ex.Message);
                    form.Close();
                }
            };

            Application.Run(form);

            var result =
                "disconnected=" + disconnected + " reason=" + reason + Environment.NewLine +
                (disconnected ? "SELFTEST PASS (state polling detected disconnect)" : "SELFTEST FAIL") +
                Environment.NewLine;
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"),
                result);
        }
    }
}
