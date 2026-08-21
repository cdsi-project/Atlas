using System.Diagnostics;
using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.WinForms;

internal sealed class AssetDetailsForm : Form
{
    private readonly AssetListItem _asset;
    private readonly DataGridView _detailsGrid = new();

    internal AssetDetailsForm(AssetListItem asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        _asset = asset;

        Text = "资产详情";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 420);
        ClientSize = new Size(780, 540);
        ShowInTaskbar = false;
        ShowIcon = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;

        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.Cancel,
            AutoSize = false,
            Size = new Size(96, 32),
            BackColor = Color.FromArgb(236, 239, 242),
            ForeColor = Color.FromArgb(31, 37, 43),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0)
        };
        closeButton.FlatAppearance.BorderSize = 0;

        var openLocationButton = new Button
        {
            Text = "打开文件位置",
            AutoSize = false,
            Size = new Size(128, 32),
            BackColor = Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = Padding.Empty
        };
        openLocationButton.FlatAppearance.BorderSize = 0;
        openLocationButton.Click += (_, _) => OpenFileLocation();

        ConfigureDetailsGrid(_detailsGrid, CreateDetailEntries(asset));
        Controls.Add(CreateLayout(
            asset.OriginalFilename,
            _detailsGrid,
            openLocationButton,
            closeButton));

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    internal static IReadOnlyList<AssetDetailEntry> CreateDetailEntries(
        AssetListItem asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return
        [
            new("资产 ID", asset.AssetId.ToString("D")),
            new("文件名", asset.OriginalFilename),
            new("标签", asset.Tags.Count == 0 ? "无标签" : string.Join("、", asset.Tags)),
            new("类型", asset.MimeType ?? "未知类型"),
            new("扩展名", string.IsNullOrWhiteSpace(asset.Extension) ? "无" : asset.Extension),
            new("大小", MainForm.FormatFileSize(asset.Size)),
            new("文件校验值（SHA-256）", asset.Sha256 ?? "未计算"),
            new("修改时间", asset.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            new("索引时间", asset.DiscoveredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
            new("本地位置", asset.Path),
            new("位置状态", MainForm.FormatLocationStatus(asset.LocationStatus)),
            new("索引状态", MainForm.FormatStatus(asset)),
            new("OSS 备份", asset.HasHealthyObjectStorageBackup ? "已备份" : "未备份"),
            new("媒体信息", MainForm.FormatMetadata(asset.Metadata))
        ];
    }

    internal static void ConfigureDetailsGrid(
        DataGridView grid,
        IReadOnlyList<AssetDetailEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(entries);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = true;
        grid.AllowUserToResizeColumns = true;
        grid.AutoGenerateColumns = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        grid.GridColor = Color.FromArgb(229, 232, 235);
        grid.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 227);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(31, 37, 43);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 242, 244);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 61, 69);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
        grid.EnableHeadersVisualStyles = false;

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Property",
            HeaderText = "属性",
            Width = 150,
            MinimumWidth = 110,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.True
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Value",
            HeaderText = "值",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 320,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.True
        });

        foreach (var entry in entries)
        {
            grid.Rows.Add(entry.Name, entry.Value);
        }
    }

    private static Control CreateLayout(
        string filename,
        DataGridView detailsGrid,
        Button openLocationButton,
        Button closeButton)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = filename,
            AutoEllipsis = true,
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = Color.FromArgb(31, 37, 43),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "本地索引记录",
            ForeColor = Color.FromArgb(101, 111, 120),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            Margin = Padding.Empty
        };
        footer.Controls.Add(closeButton);
        footer.Controls.Add(openLocationButton);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(detailsGrid, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        return layout;
    }

    private void OpenFileLocation()
    {
        if (!File.Exists(_asset.Path))
        {
            MessageBox.Show(
                this,
                $"文件当前位置不存在：{Environment.NewLine}{_asset.Path}",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var process = Process.Start(
                MainForm.CreateOpenFileLocationStartInfo(_asset.Path));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "无法打开文件位置",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

internal sealed record AssetDetailEntry(string Name, string Value);
