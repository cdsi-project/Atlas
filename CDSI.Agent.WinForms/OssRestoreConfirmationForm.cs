using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms;

internal sealed class OssRestoreConfirmationForm : Form
{
    private const string SourceColumnName = "RestoreSource";
    private const string ObjectKeyColumnName = "ObjectKey";
    private readonly RadioButton _workspaceRadioButton = new();
    private readonly RadioButton _directoryRadioButton = new();
    private readonly TextBox _directoryTextBox = new();
    private readonly Button _browseButton = new();
    private readonly DataGridView _assetsGrid = new();
    private IReadOnlyList<ObjectStorageRestoreRequest> _selectedRequests = [];

    public OssRestoreConfirmationForm(
        IReadOnlyCollection<ObjectStorageRestoreCandidate> candidates,
        string? managedWorkspacePath)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("至少需要一个可取回资产。", nameof(candidates));
        }

        Text = "从 OSS 取回资产";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 480);
        Size = new Size(900, 590);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"从 OSS 取回 {candidates.Count:N0} 个资产",
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = Color.FromArgb(31, 37, 43),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(
            CreateDestinationPanel(managedWorkspacePath),
            0,
            1);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "系统先下载到临时文件并校验大小和 SHA-256，再登记本地位置。若目标已有不同内容，操作将失败且不会覆盖。",
            ForeColor = Color.FromArgb(137, 49, 49),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 2);

        ConfigureGrid(candidates);
        layout.Controls.Add(_assetsGrid, 0, 3);
        layout.Controls.Add(CreateButtons(), 0, 4);
        Controls.Add(layout);
    }

    public IReadOnlyList<ObjectStorageRestoreRequest> SelectedRequests =>
        _selectedRequests;

    public ObjectStorageRestoreDestination Destination =>
        _workspaceRadioButton.Checked
            ? new ObjectStorageRestoreDestination(
                ObjectStorageRestoreDestinationKind.ManagedWorkspace)
            : new ObjectStorageRestoreDestination(
                ObjectStorageRestoreDestinationKind.SelectedDirectory,
                _directoryTextBox.Text.Trim());

    internal bool TryCollectSelections(
        out IReadOnlyList<ObjectStorageRestoreRequest> requests,
        out string errorMessage)
    {
        var result = new List<ObjectStorageRestoreRequest>();
        foreach (DataGridViewRow row in _assetsGrid.Rows)
        {
            if (row.Tag is not RestoreRowState state ||
                row.Cells[SourceColumnName].Value is not Guid locationId ||
                state.Choices.All(choice => choice.LocationId != locationId))
            {
                requests = [];
                errorMessage = "请为每个资产选择一个可用的 OSS 备份来源。";
                return false;
            }

            result.Add(new ObjectStorageRestoreRequest(
                state.Candidate.AssetId,
                locationId));
        }

        if (_directoryRadioButton.Checked)
        {
            if (string.IsNullOrWhiteSpace(_directoryTextBox.Text))
            {
                requests = [];
                errorMessage = "请选择恢复目录。";
                return false;
            }

            string path;
            try
            {
                path = Path.GetFullPath(_directoryTextBox.Text.Trim());
            }
            catch (Exception exception) when (exception is
                ArgumentException or NotSupportedException or PathTooLongException)
            {
                requests = [];
                errorMessage = "恢复目录路径无效。";
                return false;
            }

            if (!Directory.Exists(path))
            {
                requests = [];
                errorMessage = "所选恢复目录不存在。";
                return false;
            }
        }

        requests = result;
        errorMessage = string.Empty;
        return true;
    }

    private Control CreateDestinationPanel(string? managedWorkspacePath)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _workspaceRadioButton.Text = "CDSI 工作目录";
        _workspaceRadioButton.Dock = DockStyle.Fill;
        _workspaceRadioButton.Checked = !string.IsNullOrWhiteSpace(
            managedWorkspacePath);
        _workspaceRadioButton.Enabled = !string.IsNullOrWhiteSpace(
            managedWorkspacePath);
        _workspaceRadioButton.CheckedChanged += (_, _) => UpdateDestinationState();
        panel.Controls.Add(_workspaceRadioButton, 0, 0);
        var workspacePathLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = string.IsNullOrWhiteSpace(managedWorkspacePath)
                ? "尚未配置"
                : Path.Combine(managedWorkspacePath, "Assets"),
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(72, 81, 89),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(workspacePathLabel, 1, 0);
        panel.SetColumnSpan(workspacePathLabel, 2);

        _directoryRadioButton.Text = "用户指定目录";
        _directoryRadioButton.Dock = DockStyle.Fill;
        _directoryRadioButton.Checked = string.IsNullOrWhiteSpace(
            managedWorkspacePath);
        _directoryRadioButton.CheckedChanged += (_, _) => UpdateDestinationState();
        panel.Controls.Add(_directoryRadioButton, 0, 1);
        _directoryTextBox.Dock = DockStyle.Fill;
        _directoryTextBox.AccessibleName = "OSS 取回目录";
        panel.Controls.Add(_directoryTextBox, 1, 1);
        _browseButton.Text = "浏览...";
        _browseButton.Dock = DockStyle.Fill;
        _browseButton.Click += BrowseButton_Click;
        panel.Controls.Add(_browseButton, 2, 1);
        UpdateDestinationState();
        return panel;
    }

    private void ConfigureGrid(
        IReadOnlyCollection<ObjectStorageRestoreCandidate> candidates)
    {
        _assetsGrid.Dock = DockStyle.Fill;
        _assetsGrid.AccessibleName = "OSS 取回来源列表";
        _assetsGrid.AllowUserToAddRows = false;
        _assetsGrid.AllowUserToDeleteRows = false;
        _assetsGrid.AllowUserToResizeRows = false;
        _assetsGrid.AutoGenerateColumns = false;
        _assetsGrid.BackgroundColor = Color.White;
        _assetsGrid.BorderStyle = BorderStyle.FixedSingle;
        _assetsGrid.RowHeadersVisible = false;
        _assetsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _assetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Filename",
            HeaderText = "本地文件名",
            ReadOnly = true,
            Width = 190
        });
        _assetsGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = SourceColumnName,
            HeaderText = "OSS 来源",
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            FlatStyle = FlatStyle.Flat,
            Width = 220
        });
        _assetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ObjectKeyColumnName,
            HeaderText = "对象键",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 220
        });
        _assetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Size",
            HeaderText = "大小",
            ReadOnly = true,
            Width = 100
        });

        foreach (var candidate in candidates)
        {
            var choices = candidate.Sources
                .Where(source =>
                    source.HasStoredSecret &&
                    source.Source.Location.Status ==
                        StorageVerificationStatus.Healthy &&
                    !string.IsNullOrWhiteSpace(source.Source.Location.Sha256))
                .Select(source => new RestoreSourceChoice(source))
                .ToArray();
            if (choices.Length == 0)
            {
                throw new InvalidOperationException(
                    $"资产“{candidate.OriginalFilename}”没有可用的 OSS 备份来源。");
            }

            var rowIndex = _assetsGrid.Rows.Add();
            var row = _assetsGrid.Rows[rowIndex];
            row.Tag = new RestoreRowState(candidate, choices);
            row.Cells["Filename"].Value = candidate.OriginalFilename;
            var sourceCell = new DataGridViewComboBoxCell
            {
                DataSource = choices,
                DisplayMember = nameof(RestoreSourceChoice.DisplayName),
                ValueMember = nameof(RestoreSourceChoice.LocationId),
                Value = choices[0].LocationId,
                FlatStyle = FlatStyle.Flat
            };
            row.Cells[SourceColumnName] = sourceCell;
            row.Cells[ObjectKeyColumnName].Value = choices[0].Source.Source.Location.ObjectKey;
            row.Cells["Size"].Value = FormatFileSize(
                choices[0].Source.Source.Location.Size);
        }

        _assetsGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_assetsGrid.IsCurrentCellDirty)
            {
                _assetsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _assetsGrid.CellValueChanged += (_, args) =>
        {
            if (args.RowIndex < 0 ||
                _assetsGrid.Columns[args.ColumnIndex].Name != SourceColumnName)
            {
                return;
            }

            UpdateSourceDetails(_assetsGrid.Rows[args.RowIndex]);
        };
    }

    private Control CreateButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        var confirmButton = new Button
        {
            Text = "开始取回",
            Size = new Size(104, 32),
            BackColor = Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        confirmButton.FlatAppearance.BorderSize = 0;
        confirmButton.Click += ConfirmButton_Click;
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        panel.Controls.Add(confirmButton);
        panel.Controls.Add(cancelButton);
        AcceptButton = confirmButton;
        CancelButton = cancelButton;
        return panel;
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (!TryCollectSelections(out var requests, out var errorMessage))
        {
            MessageBox.Show(
                this,
                errorMessage,
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _selectedRequests = requests;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 OSS 资产取回目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(_directoryTextBox.Text)
                ? _directoryTextBox.Text
                : string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _directoryTextBox.Text = dialog.SelectedPath;
            _directoryRadioButton.Checked = true;
        }
    }

    private void UpdateDestinationState()
    {
        _directoryTextBox.Enabled = _directoryRadioButton.Checked;
        _browseButton.Enabled = _directoryRadioButton.Checked;
    }

    private void UpdateSourceDetails(DataGridViewRow row)
    {
        if (row.Tag is not RestoreRowState state ||
            row.Cells[SourceColumnName].Value is not Guid locationId)
        {
            return;
        }

        var choice = state.Choices.SingleOrDefault(item =>
            item.LocationId == locationId);
        if (choice is null)
        {
            return;
        }

        row.Cells[ObjectKeyColumnName].Value =
            choice.Source.Source.Location.ObjectKey;
        row.Cells["Size"].Value = FormatFileSize(
            choice.Source.Source.Location.Size);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private sealed record RestoreSourceChoice(
        ConfiguredObjectStorageRestoreSource Source)
    {
        public Guid LocationId => Source.Source.Location.Id;

        public string DisplayName =>
            $"{Source.Profile.DisplayName} ({Source.Profile.BucketName})";
    }

    private sealed record RestoreRowState(
        ObjectStorageRestoreCandidate Candidate,
        IReadOnlyList<RestoreSourceChoice> Choices);
}
