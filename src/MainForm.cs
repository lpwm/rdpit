using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpIt
{
    /// <summary>
    /// Connection manager: lists saved profiles and lets you add/edit/delete them.
    /// Double-clicking (or "Connect") opens a session using the stored credentials
    /// with no further prompts.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly ConnectionStore _store = new ConnectionStore();
        private readonly SettingsStore _settingsStore = new SettingsStore();
        private readonly ListView _list = new ListView();

        public MainForm()
        {
            BuildUi();
            _store.Load();
            _settingsStore.Load();
            RefreshList();
        }

        private void BuildUi()
        {
            Text = "RdpIt — Simple and friendly RDP client";
            ClientSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(480, 320);
            Font = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.Dock = DockStyle.Fill;
            _list.HideSelection = false;
            _list.Columns.Add("Name", 170);
            _list.Columns.Add("Host", 170);
            _list.Columns.Add("Port", 60);
            _list.Columns.Add("Username", 150);
            _list.DoubleClick += (s, e) => ConnectSelected();
            _list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { ConnectSelected(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            };
            _list.ContextMenuStrip = BuildContextMenu();

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 8, 8, 4),
            };
            toolbar.Controls.Add(MakeButton("Connect", (s, e) => ConnectSelected(), true));
            toolbar.Controls.Add(MakeButton("New", (s, e) => AddNew()));
            toolbar.Controls.Add(MakeButton("Edit", (s, e) => EditSelected()));
            toolbar.Controls.Add(MakeButton("Delete", (s, e) => DeleteSelected()));
            toolbar.Controls.Add(MakeButton("Settings", (s, e) => OpenSettings()));

            var status = new StatusStrip();
            var hint = new ToolStripStatusLabel("Double-click a connection to connect.")
            {
                TextAlign = ContentAlignment.MiddleLeft
            };
            // Spring spacer pushes the version label to the right-hand corner while
            // leaving the version always visible (the hint no longer eats all width).
            var spacer = new ToolStripStatusLabel
            {
                Spring = true,
                Text = string.Empty
            };
            var version = new ToolStripStatusLabel(AppVersion.Label)
            {
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = SystemColors.GrayText,
                ToolTipText = "RdpIt " + AppVersion.Label
            };
            status.Items.Add(hint);
            status.Items.Add(spacer);
            status.Items.Add(version);

            Controls.Add(_list);
            Controls.Add(toolbar);
            Controls.Add(status);
        }

        private Button MakeButton(string text, EventHandler onClick, bool emphasize = false)
        {
            var b = new Button { Text = text, Width = 90, Height = 28, Margin = new Padding(3) };
            if (emphasize) b.Font = new Font(Font, FontStyle.Bold);
            b.Click += onClick;
            return b;
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            var connect = new ToolStripMenuItem("Connect", null, (s, e) => ConnectSelected());
            connect.Font = new Font(menu.Font, FontStyle.Bold);
            var edit = new ToolStripMenuItem("Edit...", null, (s, e) => EditSelected());
            var shortcut = new ToolStripMenuItem("Create shortcut", null, (s, e) => CreateShortcutForSelected());
            var delete = new ToolStripMenuItem("Delete", null, (s, e) => DeleteSelected());

            menu.Items.Add(connect);
            menu.Items.Add(edit);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(shortcut);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(delete);

            // Only enable items when a row is actually selected.
            menu.Opening += (s, e) =>
            {
                bool has = Selected() != null;
                connect.Enabled = has;
                edit.Enabled = has;
                shortcut.Enabled = has;
                delete.Enabled = has;
                if (!has) e.Cancel = true; // don't show an all-disabled menu
            };
            return menu;
        }

        private void CreateShortcutForSelected()
        {
            var c = Selected();
            if (c == null) return;
            try
            {
                var path = ShortcutService.CreateDesktopShortcut(c);
                MessageBox.Show(this,
                    "Shortcut created on your desktop:\n\n" + path +
                    "\n\nDouble-click it to connect directly to this saved connection.",
                    "Shortcut created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not create the shortcut:\n\n" + ex.Message,
                    "Shortcut error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var c in _store.Connections)
            {
                var item = new ListViewItem(string.IsNullOrWhiteSpace(c.Name) ? c.Host : c.Name)
                {
                    Tag = c
                };
                item.SubItems.Add(c.Host);
                item.SubItems.Add(c.Port.ToString());
                item.SubItems.Add(c.Username);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0 && _list.SelectedItems.Count == 0)
                _list.Items[0].Selected = true;
        }

        private Connection Selected()
        {
            return _list.SelectedItems.Count > 0
                ? (Connection)_list.SelectedItems[0].Tag
                : null;
        }

        private void AddNew()
        {
            using (var dlg = new ConnectionEditForm(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                {
                    _store.Upsert(dlg.Result);
                    _store.Save();
                    RefreshList();
                }
            }
        }

        private void EditSelected()
        {
            var c = Selected();
            if (c == null) return;
            using (var dlg = new ConnectionEditForm(c))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                {
                    _store.Upsert(dlg.Result);
                    _store.Save();
                    RefreshList();
                }
            }
        }

        private void DeleteSelected()
        {
            var c = Selected();
            if (c == null) return;
            var answer = MessageBox.Show(this,
                "Delete connection \"" + (string.IsNullOrWhiteSpace(c.Name) ? c.Host : c.Name) + "\"?",
                "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes)
            {
                _store.Remove(c.Id);
                _store.Save();
                RefreshList();
            }
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(_settingsStore.Settings))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                {
                    _settingsStore.Save(dlg.Result);
                }
            }
        }

        private void ConnectSelected()
        {
            var c = Selected();
            if (c == null)
            {
                MessageBox.Show(this, "Select a connection first.", "No selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // Open the session using stored credentials — no prompt.
            var session = new SessionForm(c, _settingsStore.Settings, _settingsStore, _store);
            session.Show(this);
        }
    }
}
