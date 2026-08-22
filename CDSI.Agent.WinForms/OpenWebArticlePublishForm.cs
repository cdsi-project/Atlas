using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.WinForms;

public sealed class OpenWebArticlePublishForm : Form
{
    private readonly TextBox _titleTextBox = new();
    private readonly ComboBox _sourceComboBox = new();
    private readonly ComboBox _statusComboBox = new();

    public OpenWebArticlePublishForm(
        string title,
        string sourcePath,
        IReadOnlyList<ConfiguredOpenWebSource> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new ArgumentException("至少需要一个 OpenWeb 源站。", nameof(sources));
        }

        Text = "发布到 OpenWeb";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 342);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 2,
            RowCount = 9
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = "发布文章",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Color.FromArgb(31, 37, 43)
        };
        layout.Controls.Add(heading, 0, 0);
        layout.SetColumnSpan(heading, 2);

        var sourceLabel = new Label
        {
            Text = Path.GetFullPath(sourcePath),
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(88, 98, 106),
            AccessibleName = "文章源文件"
        };
        layout.Controls.Add(sourceLabel, 0, 1);
        layout.SetColumnSpan(sourceLabel, 2);

        var titleLabel = new Label
        {
            Text = "标题",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(72, 81, 89)
        };
        layout.Controls.Add(titleLabel, 0, 2);
        layout.SetColumnSpan(titleLabel, 2);

        _titleTextBox.Text = title;
        _titleTextBox.MaxLength = 200;
        _titleTextBox.Dock = DockStyle.Fill;
        _titleTextBox.Margin = new Padding(0, 4, 0, 5);
        _titleTextBox.AccessibleName = "OpenWeb 文章标题";
        layout.Controls.Add(_titleTextBox, 0, 3);
        layout.SetColumnSpan(_titleTextBox, 2);

        var targetLabel = new Label
        {
            Text = "目标源站",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(72, 81, 89)
        };
        layout.Controls.Add(targetLabel, 0, 4);
        layout.SetColumnSpan(targetLabel, 2);

        _sourceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sourceComboBox.Dock = DockStyle.Fill;
        _sourceComboBox.Margin = new Padding(0, 4, 0, 5);
        _sourceComboBox.AccessibleName = "OpenWeb 目标源站";
        foreach (var configured in sources)
        {
            _sourceComboBox.Items.Add(new SourceOption(configured));
        }

        _sourceComboBox.SelectedIndex = Math.Max(
            0,
            sources.Select((configured, index) => (configured, index))
                .FirstOrDefault(item => item.configured.Source.IsDefault)
                .index);
        layout.Controls.Add(_sourceComboBox, 0, 5);
        layout.SetColumnSpan(_sourceComboBox, 2);

        var statusLabel = new Label
        {
            Text = "发布状态",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(72, 81, 89)
        };
        layout.Controls.Add(statusLabel, 0, 6);
        layout.SetColumnSpan(statusLabel, 2);

        _statusComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusComboBox.Dock = DockStyle.Fill;
        _statusComboBox.Margin = new Padding(0, 4, 8, 5);
        _statusComboBox.AccessibleName = "OpenWeb 发布状态";
        _statusComboBox.Items.AddRange(
            [
                new PublishStatusOption("保存为草稿", OpenWebArticleStatus.Draft),
                new PublishStatusOption("立即发布", OpenWebArticleStatus.Published)
            ]);
        _statusComboBox.SelectedIndex = 0;
        layout.Controls.Add(_statusComboBox, 0, 7);

        var publishButton = new Button
        {
            Text = "发布",
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 4, 0, 5),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            AccessibleName = "确认发布到 OpenWeb"
        };
        publishButton.FlatAppearance.BorderSize = 0;
        publishButton.Click += (_, _) => Confirm();
        layout.Controls.Add(publishButton, 1, 7);

        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Size = new Size(96, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(236, 239, 242),
            ForeColor = Color.FromArgb(31, 37, 43)
        };
        cancelButton.FlatAppearance.BorderSize = 0;
        layout.Controls.Add(cancelButton, 1, 8);

        Controls.Add(layout);
        AcceptButton = publishButton;
        CancelButton = cancelButton;
    }

    public string ArticleTitle => _titleTextBox.Text.Trim();

    public Guid SourceId =>
        (_sourceComboBox.SelectedItem as SourceOption)?.Configured.Source.Id
        ?? Guid.Empty;

    public OpenWebArticleStatus ArticleStatus =>
        (_statusComboBox.SelectedItem as PublishStatusOption)?.Status
        ?? OpenWebArticleStatus.Draft;

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(ArticleTitle))
        {
            MessageBox.Show(
                this,
                "必须填写文章标题。",
                "CDSI Beacon",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _titleTextBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record PublishStatusOption(
        string DisplayName,
        OpenWebArticleStatus Status)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed record SourceOption(ConfiguredOpenWebSource Configured)
    {
        public override string ToString()
        {
            var credentialStatus = Configured.HasApplicationPassword
                ? string.Empty
                : "（凭据缺失）";
            return $"{Configured.Source.DisplayName} · {Configured.Source.OriginDomain}{credentialStatus}";
        }
    }
}
