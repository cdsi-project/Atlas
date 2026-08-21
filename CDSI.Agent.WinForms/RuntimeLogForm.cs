using System.Diagnostics;

namespace CDSI.Agent.WinForms;

internal sealed class RuntimeLogForm : Form
{
    private readonly RuntimeLogService _runtimeLog;
    private readonly ComboBox _logFileComboBox = new();
    private readonly RichTextBox _logTextBox = new();
    private readonly Label _pathLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    public RuntimeLogForm(RuntimeLogService runtimeLog)
    {
        _runtimeLog = runtimeLog ?? throw new ArgumentNullException(nameof(runtimeLog));
        Text = "运行日志";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 620);
        MinimumSize = new Size(680, 420);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            Margin = Padding.Empty
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        toolbar.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "日志文件",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _logFileComboBox.Dock = DockStyle.Fill;
        _logFileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _logFileComboBox.AccessibleName = "日志文件";
        _logFileComboBox.Margin = new Padding(0, 5, 8, 5);
        _logFileComboBox.SelectedIndexChanged += (_, _) => RefreshLogContent();
        toolbar.Controls.Add(_logFileComboBox, 1, 0);

        var refreshButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "刷新",
            Margin = new Padding(0, 4, 8, 4)
        };
        refreshButton.Click += (_, _) => ReloadLogFiles();
        toolbar.Controls.Add(refreshButton, 2, 0);

        var openDirectoryButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "打开日志目录",
            Margin = new Padding(0, 4, 0, 4)
        };
        openDirectoryButton.Click += (_, _) => OpenLogDirectory();
        toolbar.Controls.Add(openDirectoryButton, 3, 0);
        layout.Controls.Add(toolbar, 0, 0);

        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.ReadOnly = true;
        _logTextBox.WordWrap = false;
        _logTextBox.DetectUrls = false;
        _logTextBox.BackColor = Color.White;
        _logTextBox.BorderStyle = BorderStyle.FixedSingle;
        _logTextBox.Font = new Font("Consolas", 9F);
        _logTextBox.AccessibleName = "日志内容";
        layout.Controls.Add(_logTextBox, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 8, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.AutoEllipsis = true;
        _pathLabel.ForeColor = Color.FromArgb(112, 121, 129);
        _pathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pathLabel.AccessibleName = "日志路径";
        var closeButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "关闭",
            DialogResult = DialogResult.Cancel
        };
        footer.Controls.Add(_pathLabel, 0, 0);
        footer.Controls.Add(closeButton, 1, 0);
        layout.Controls.Add(footer, 0, 2);

        Controls.Add(layout);
        CancelButton = closeButton;

        _refreshTimer.Interval = 2000;
        _refreshTimer.Tick += (_, _) => RefreshLogContent();
        Shown += (_, _) =>
        {
            ReloadLogFiles();
            _refreshTimer.Start();
        };
        FormClosed += (_, _) =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        };
    }

    internal int LogFileCount => _logFileComboBox.Items.Count;

    internal string LogContent => _logTextBox.Text;

    internal void ReloadLogFiles()
    {
        var selectedPath = (_logFileComboBox.SelectedItem as LogFileItem)?.FullPath;
        var logFiles = _runtimeLog.GetLogFiles();
        _logFileComboBox.BeginUpdate();
        try
        {
            _logFileComboBox.Items.Clear();
            _logFileComboBox.Items.AddRange(
                logFiles.Select(path => (object)new LogFileItem(path)).ToArray());
            if (_logFileComboBox.Items.Count == 0)
            {
                _logTextBox.Text = "暂无运行日志。";
                _pathLabel.Text = _runtimeLog.LogDirectory;
                return;
            }

            var pathToSelect = logFiles.FirstOrDefault(path =>
                    string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase))
                ?? logFiles.FirstOrDefault(path =>
                    string.Equals(
                        path,
                        _runtimeLog.CurrentLogPath,
                        StringComparison.OrdinalIgnoreCase))
                ?? logFiles[0];
            _logFileComboBox.SelectedIndex = logFiles
                .Select((path, index) => (path, index))
                .First(item => string.Equals(
                    item.path,
                    pathToSelect,
                    StringComparison.OrdinalIgnoreCase))
                .index;
        }
        finally
        {
            _logFileComboBox.EndUpdate();
        }

        RefreshLogContent();
    }

    private void RefreshLogContent()
    {
        if (_logFileComboBox.SelectedItem is not LogFileItem logFile)
        {
            return;
        }

        var logPath = logFile.FullPath;
        try
        {
            var content = _runtimeLog.ReadLogFile(logPath);
            if (!string.Equals(_logTextBox.Text, content, StringComparison.Ordinal))
            {
                var wasAtEnd = _logTextBox.SelectionStart >= _logTextBox.TextLength - 1;
                var selectionStart = _logTextBox.SelectionStart;
                _logTextBox.Text = content;
                _logTextBox.SelectionStart = wasAtEnd
                    ? _logTextBox.TextLength
                    : Math.Min(selectionStart, _logTextBox.TextLength);
                _logTextBox.ScrollToCaret();
            }

            _pathLabel.Text = logPath;
        }
        catch (Exception exception)
        {
            _pathLabel.Text = logPath;
            _logTextBox.Text = $"无法读取日志：{exception.Message}";
        }
    }

    private void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_runtimeLog.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _runtimeLog.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            _runtimeLog.WriteError("无法打开日志目录", exception);
            MessageBox.Show(
                this,
                exception.Message,
                "无法打开日志目录",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private sealed record LogFileItem(string FullPath)
    {
        public override string ToString()
        {
            return Path.GetFileName(FullPath);
        }
    }
}
