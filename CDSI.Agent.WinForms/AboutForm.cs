using System.Diagnostics;

namespace CDSI.Agent.WinForms;

internal sealed class AboutForm : Form
{
    internal const string RepositoryUrl = "https://github.com/cdsi-project/Atlas";

    public AboutForm(string version)
    {
        Text = "关于 CDSI Atlas";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "CDSI Atlas",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Color.FromArgb(31, 37, 43)
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"版本 {version}",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(88, 98, 106),
            AccessibleName = "应用版本"
        }, 0, 1);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Copyright (c) 2026 CDSI Project · Apache-2.0",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        }, 0, 2);
        var repositoryLink = new LinkLabel
        {
            Dock = DockStyle.Fill,
            Text = "GitHub: github.com/cdsi-project/Atlas",
            TextAlign = ContentAlignment.MiddleLeft,
            LinkColor = Color.FromArgb(24, 121, 78),
            AccessibleName = "CDSI Atlas GitHub 仓库"
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
        layout.Controls.Add(repositoryLink, 0, 3);
        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Size = new Size(88, 32)
        };
        layout.Controls.Add(closeButton, 0, 4);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(layout);
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
