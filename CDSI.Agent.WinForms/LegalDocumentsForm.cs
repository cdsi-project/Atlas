using System.Diagnostics;

namespace CDSI.Agent.WinForms;

internal enum LegalDocumentPage
{
    OpenSourceLicense,
    ThirdPartyNotices
}

internal sealed class LegalDocumentsForm : Form
{
    public LegalDocumentsForm(
        string licenseText,
        string thirdPartyNoticesText,
        LegalDocumentPage initialPage)
    {
        ArgumentNullException.ThrowIfNull(licenseText);
        ArgumentNullException.ThrowIfNull(thirdPartyNoticesText);

        Text = "许可信息";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 680);
        MinimumSize = new Size(720, 520);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 5),
            AccessibleName = "许可文档"
        };
        tabs.TabPages.Add(CreateDocumentPage(
            "开源协议",
            licenseText,
            "Apache License 2.0 完整文本"));
        tabs.TabPages.Add(CreateDocumentPage(
            "第三方许可",
            thirdPartyNoticesText,
            "第三方许可完整文本"));
        tabs.SelectedIndex = initialPage == LegalDocumentPage.OpenSourceLicense
            ? 0
            : 1;

        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right,
            Size = new Size(88, 32)
        };
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.White
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        footer.Controls.Add(closeButton, 1, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.Controls.Add(tabs, 0, 0);
        layout.Controls.Add(footer, 0, 1);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(layout);
    }

    public static LegalDocumentsForm LoadFromDirectory(
        string baseDirectory,
        LegalDocumentPage initialPage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        var fullBaseDirectory = Path.GetFullPath(baseDirectory);
        return new LegalDocumentsForm(
            ReadRequiredDocument(fullBaseDirectory, "LICENSE"),
            ReadRequiredDocument(fullBaseDirectory, "THIRD-PARTY-NOTICES.md"),
            initialPage);
    }

    private static TabPage CreateDocumentPage(
        string title,
        string content,
        string accessibleName)
    {
        var viewer = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            DetectUrls = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(31, 37, 43),
            Font = new Font("Consolas", 9.5F),
            Text = content,
            AccessibleName = accessibleName
        };
        viewer.LinkClicked += (_, args) => OpenLink(viewer, args.LinkText);
        viewer.SelectionStart = 0;
        viewer.SelectionLength = 0;

        var page = new TabPage(title)
        {
            Padding = new Padding(8),
            BackColor = Color.White
        };
        page.Controls.Add(viewer);
        return page;
    }

    private static string ReadRequiredDocument(string baseDirectory, string filename)
    {
        var path = Path.Combine(baseDirectory, filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("应用许可文档不存在。", path);
        }

        return File.ReadAllText(path);
    }

    private static void OpenLink(IWin32Window owner, string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = link,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                owner,
                exception.Message,
                "无法打开链接",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
