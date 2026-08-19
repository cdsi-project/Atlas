using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms;

internal sealed partial class OssBackupConfirmationForm
{
    public IReadOnlyDictionary<Guid, string> SelectedObjectNames =>
        _selectedObjectNames;

    private void ConfigureAssetGrid(IReadOnlyCollection<AssetListItem> assets)
    {
        _assetsGrid.Dock = DockStyle.Fill;
        _assetsGrid.AccessibleName = "OSS 备份文件名列表";
        _assetsGrid.AllowUserToAddRows = false;
        _assetsGrid.AllowUserToDeleteRows = false;
        _assetsGrid.AllowUserToOrderColumns = false;
        _assetsGrid.AllowUserToResizeRows = false;
        _assetsGrid.AutoGenerateColumns = false;
        _assetsGrid.BackgroundColor = Color.White;
        _assetsGrid.BorderStyle = BorderStyle.FixedSingle;
        _assetsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _assetsGrid.ColumnHeadersHeight = 32;
        _assetsGrid.ColumnHeadersHeightSizeMode =
            DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _assetsGrid.EditMode = DataGridViewEditMode.EditOnEnter;
        _assetsGrid.MultiSelect = false;
        _assetsGrid.RowHeadersVisible = false;
        _assetsGrid.RowTemplate.Height = 30;
        _assetsGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;

        _assetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "LocalPath",
            HeaderText = "本地文件",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 62,
            MinimumWidth = 280,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _assetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ObjectNameColumnName,
            HeaderText = "OSS 文件名",
            ReadOnly = false,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 38,
            MinimumWidth = 200,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        foreach (var asset in assets)
        {
            var localFilename = Path.GetFileName(asset.Path);
            if (string.IsNullOrWhiteSpace(localFilename))
            {
                localFilename = asset.OriginalFilename;
            }

            var rowIndex = _assetsGrid.Rows.Add(asset.Path, localFilename);
            var row = _assetsGrid.Rows[rowIndex];
            row.Tag = asset.AssetId;
            row.Cells["LocalPath"].ToolTipText = asset.Path;
        }
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        _assetsGrid.EndEdit();
        if (!TryCollectObjectNames(
                out var objectNames,
                out var errorMessage,
                out var invalidCell))
        {
            if (invalidCell is not null)
            {
                _assetsGrid.CurrentCell = invalidCell;
            }

            MessageBox.Show(
                this,
                errorMessage,
                "无法开始 OSS 备份",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _assetsGrid.Focus();
            _assetsGrid.BeginEdit(true);
            return;
        }

        _selectedObjectNames = objectNames;
        DialogResult = DialogResult.OK;
        Close();
    }

    internal bool TryCollectObjectNames(
        out IReadOnlyDictionary<Guid, string> objectNames,
        out string errorMessage)
    {
        return TryCollectObjectNames(
            out objectNames,
            out errorMessage,
            out _);
    }

    private bool TryCollectObjectNames(
        out IReadOnlyDictionary<Guid, string> objectNames,
        out string errorMessage,
        out DataGridViewCell? invalidCell)
    {
        var result = new Dictionary<Guid, string>();
        foreach (DataGridViewRow row in _assetsGrid.Rows)
        {
            if (row.IsNewRow || row.Tag is not Guid assetId)
            {
                continue;
            }

            var cell = row.Cells[ObjectNameColumnName];
            var filename = Convert.ToString(cell.Value);
            if (!ObjectStorageObjectKey.TryCreateForAsset(
                    assetId,
                    filename,
                    out _,
                    out var validationError))
            {
                var localFilename = Path.GetFileName(
                    Convert.ToString(row.Cells["LocalPath"].Value));
                objectNames = result;
                errorMessage = $"{localFilename}: {validationError}";
                invalidCell = cell;
                return false;
            }

            result[assetId] = filename!;
        }

        objectNames = result;
        errorMessage = string.Empty;
        invalidCell = null;
        return true;
    }
}
