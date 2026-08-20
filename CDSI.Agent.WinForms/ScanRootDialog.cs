using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.WinForms;

public sealed class ScanRootDialog : Form
{
    private static readonly IReadOnlyList<FileTypeChoice> FileTypeChoices =
    [
        new(AssetFileTypeFilter.All, "全部类型"),
        new(AssetFileTypeFilter.Video, "视频"),
        new(AssetFileTypeFilter.Audio, "音频"),
        new(AssetFileTypeFilter.Image, "图片"),
        new(AssetFileTypeFilter.Document, "文档"),
        new(AssetFileTypeFilter.Other, "其他")
    ];

    private readonly TextBox _pathTextBox = new();
    private readonly ComboBox _fileTypeComboBox = new();
    private readonly bool _requireAvailablePath;

    public ScanRootDialog(
        string? path = null,
        AssetFileTypeFilter fileTypeFilter = AssetFileTypeFilter.All,
        bool allowPathSelection = true)
    {
        if (!Enum.IsDefined(fileTypeFilter))
        {
            throw new ArgumentOutOfRangeException(nameof(fileTypeFilter));
        }

        _requireAvailablePath = allowPathSelection;

        Text = allowPathSelection ? "添加扫描目录" : "设置扫描目录";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 205);
        MinimumSize = new Size(560, 205);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(20, 18, 20, 16)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var directoryLabel = CreateLabel("扫描目录");
        layout.Controls.Add(directoryLabel, 0, 0);
        layout.SetColumnSpan(directoryLabel, 3);

        _pathTextBox.Dock = DockStyle.Fill;
        _pathTextBox.Margin = new Padding(0, 5, 8, 5);
        _pathTextBox.Text = path ?? string.Empty;
        _pathTextBox.ReadOnly = !allowPathSelection;
        _pathTextBox.AccessibleName = "扫描目录路径";
        layout.Controls.Add(_pathTextBox, 0, 1);
        layout.SetColumnSpan(_pathTextBox, 2);

        var browseButton = CreateButton("浏览", Color.FromArgb(236, 239, 242), Color.FromArgb(31, 37, 43));
        browseButton.Enabled = allowPathSelection;
        browseButton.Margin = new Padding(4, 5, 0, 5);
        browseButton.Click += BrowseButton_Click;
        layout.Controls.Add(browseButton, 2, 1);

        _fileTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _fileTypeComboBox.Width = 180;
        _fileTypeComboBox.Margin = new Padding(8, 5, 0, 5);
        _fileTypeComboBox.AccessibleName = "扫描文件类型";
        _fileTypeComboBox.Items.AddRange(FileTypeChoices.Cast<object>().ToArray());
        _fileTypeComboBox.SelectedItem = FileTypeChoices.Single(
            choice => choice.Value == fileTypeFilter);

        var fileTypePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var fileTypeLabel = CreateLabel("文件类型");
        fileTypeLabel.Width = 84;
        fileTypeLabel.Margin = new Padding(0, 5, 0, 5);
        fileTypePanel.Controls.Add(fileTypeLabel);
        fileTypePanel.Controls.Add(_fileTypeComboBox);
        layout.Controls.Add(fileTypePanel, 0, 2);
        layout.SetColumnSpan(fileTypePanel, 3);

        var confirmButton = CreateButton(
            allowPathSelection ? "添加" : "保存",
            Color.FromArgb(24, 121, 78),
            Color.White);
        confirmButton.Size = new Size(96, 32);
        confirmButton.Click += ConfirmButton_Click;

        var cancelButton = CreateButton(
            "取消",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));
        cancelButton.Size = new Size(96, 32);
        cancelButton.DialogResult = DialogResult.Cancel;

        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        commands.Controls.Add(confirmButton);
        commands.Controls.Add(cancelButton);
        layout.Controls.Add(commands, 0, 3);
        layout.SetColumnSpan(commands, 3);

        Controls.Add(layout);
        AcceptButton = confirmButton;
        CancelButton = cancelButton;
    }

    public string SelectedPath => Path.GetFullPath(_pathTextBox.Text.Trim());

    public AssetFileTypeFilter FileTypeFilter =>
        (_fileTypeComboBox.SelectedItem as FileTypeChoice)?.Value
        ?? AssetFileTypeFilter.All;

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择只读扫描目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = Directory.Exists(_pathTextBox.Text)
                ? _pathTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_pathTextBox.Text) ||
            (_requireAvailablePath && !Directory.Exists(_pathTextBox.Text.Trim())))
        {
            MessageBox.Show(
                this,
                "请选择当前可用的扫描目录。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(52, 61, 69)
        };
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        return new Button
        {
            Text = text,
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
    }

    private sealed record FileTypeChoice(
        AssetFileTypeFilter Value,
        string Label)
    {
        public override string ToString() => Label;
    }
}
