using System.Diagnostics;

namespace CDSI.Agent.WinForms;

internal sealed class AboutForm : Form
{
    internal const string RepositoryUrl = "https://github.com/cdsi-project/Beacon";

    public AboutForm(string version, string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        Text = "关于 CDSI Beacon";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(24),
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logoImage = LoadLogoImage(AppContext.BaseDirectory);
        var logoPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = logoImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 20, 0),
            AccessibleName = "CDSI Beacon 标识"
        };
        layout.Controls.Add(logoPictureBox, 0, 0);
        layout.SetRowSpan(logoPictureBox, 5);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "CDSI Beacon",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Color.FromArgb(31, 37, 43)
        }, 1, 0);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"版本 {version}",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(88, 98, 106),
            AccessibleName = "应用版本"
        }, 1, 1);

        var clientIdentityLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = Color.White
        };
        clientIdentityLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));
        clientIdentityLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 72));
        clientIdentityLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 24));
        clientIdentityLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 32));
        var clientIdentityLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "客户端 ID",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(88, 98, 106)
        };
        clientIdentityLayout.Controls.Add(clientIdentityLabel, 0, 0);
        clientIdentityLayout.SetColumnSpan(clientIdentityLabel, 2);
        clientIdentityLayout.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill,
            Text = clientId,
            ReadOnly = true,
            TabStop = false,
            AccessibleName = "客户端 ID"
        }, 0, 1);
        var copyClientIdButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "复制",
            Margin = new Padding(8, 0, 0, 0),
            AccessibleName = "复制客户端 ID"
        };
        copyClientIdButton.Click += (_, _) => CopyClientId(clientId);
        clientIdentityLayout.Controls.Add(copyClientIdButton, 1, 1);
        layout.Controls.Add(clientIdentityLayout, 1, 2);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Copyright (c) 2026 CDSI Project · Apache-2.0",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        }, 1, 3);
        var repositoryLink = new LinkLabel
        {
            Dock = DockStyle.Fill,
            Text = "GitHub: github.com/cdsi-project/Beacon",
            TextAlign = ContentAlignment.MiddleLeft,
            LinkColor = Color.FromArgb(24, 121, 78),
            AccessibleName = "CDSI Beacon GitHub 仓库"
        };
        repositoryLink.LinkClicked += (_, _) =>
        {
            try
            {
                using var process = Process.Start(CreateRepositoryStartInfo());
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "无法打开项目主页",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        layout.Controls.Add(repositoryLink, 1, 4);
        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Size = new Size(88, 32)
        };
        layout.Controls.Add(closeButton, 1, 5);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(layout);
        FormClosed += (_, _) =>
        {
            logoPictureBox.Image = null;
            logoImage?.Dispose();
        };
    }

    internal static Image? LoadLogoImage(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        var path = Path.Combine(Path.GetFullPath(baseDirectory), "logo.png");
        if (!File.Exists(path))
        {
            return null;
        }

        using var source = Image.FromFile(path);
        return new Bitmap(source);
    }

    private void CopyClientId(string clientId)
    {
        try
        {
            Clipboard.SetText(clientId);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "无法复制客户端 ID",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    internal static ProcessStartInfo CreateRepositoryStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = RepositoryUrl,
            UseShellExecute = true
        };
    }
}
