using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpIt
{
    /// <summary>Add / edit dialog for a single connection profile.</summary>
    public sealed class ConnectionEditForm : Form
    {
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _host = new TextBox();
        private readonly NumericUpDown _port = new NumericUpDown();
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _domain = new TextBox();
        private readonly TextBox _password = new TextBox();
        private readonly CheckBox _showPassword = new CheckBox();

        private readonly Connection _original;

        public Connection Result { get; private set; }

        public ConnectionEditForm(Connection existing)
        {
            _original = existing;
            BuildUi();
            if (existing != null) LoadFrom(existing);
        }

        private void BuildUi()
        {
            Text = _original == null ? "New Connection" : "Edit Connection";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 300);
            Font = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(12),
                AutoSize = false,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(layout, "Display name:", _name);
            AddRow(layout, "Host / IP:", _host);

            _port.Minimum = 1;
            _port.Maximum = 65535;
            _port.Value = 3389;
            _port.Width = 90;
            AddRow(layout, "Port:", _port);

            AddRow(layout, "Username:", _user);
            AddRow(layout, "Domain (opt):", _domain);

            _password.UseSystemPasswordChar = true;
            AddRow(layout, "Password:", _password);

            _showPassword.Text = "Show password";
            _showPassword.CheckedChanged += (s, e) =>
                _password.UseSystemPasswordChar = !_showPassword.Checked;
            layout.Controls.Add(new Label(), 0, layout.RowCount);
            layout.Controls.Add(_showPassword, 1, layout.RowCount);
            layout.RowCount++;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(8),
            };
            var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            ok.Click += OnSave;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void AddRow(TableLayoutPanel layout, string label, Control input)
        {
            input.Dock = input is NumericUpDown ? DockStyle.Left : DockStyle.Fill;
            input.Margin = new Padding(3, 4, 3, 4);
            var lbl = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 7, 3, 4),
            };
            layout.Controls.Add(lbl, 0, layout.RowCount);
            layout.Controls.Add(input, 1, layout.RowCount);
            layout.RowCount++;
        }

        private void LoadFrom(Connection c)
        {
            _name.Text = c.Name;
            _host.Text = c.Host;
            _port.Value = c.Port >= _port.Minimum && c.Port <= _port.Maximum ? c.Port : 3389;
            _user.Text = c.Username;
            _domain.Text = c.Domain;
            _password.Text = ConnectionStore.Unprotect(c.EncryptedPassword);
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_host.Text))
            {
                MessageBox.Show(this, "Host / IP is required.", "Missing field",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            var c = _original != null ? CloneId(_original) : new Connection();
            c.Name = _name.Text.Trim();
            c.Host = _host.Text.Trim();
            c.Port = (int)_port.Value;
            c.Username = _user.Text.Trim();
            c.Domain = _domain.Text.Trim();
            c.EncryptedPassword = ConnectionStore.Protect(_password.Text);
            Result = c;
        }

        private static Connection CloneId(Connection src)
        {
            // Preserve fields the edit dialog doesn't expose (e.g. the remembered
            // per-connection window size) so editing doesn't discard them.
            return new Connection
            {
                Id = src.Id,
                LastWindowWidth = src.LastWindowWidth,
                LastWindowHeight = src.LastWindowHeight
            };
        }
    }
}
