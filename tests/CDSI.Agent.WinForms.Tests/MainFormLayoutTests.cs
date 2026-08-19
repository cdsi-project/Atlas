using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class MainFormLayoutTests
{
    [Fact]
    public void CreateStatisticsPanel_ReservesVisibleValueRows()
    {
        Label[] valueLabels = [new(), new(), new(), new()];
        using var panel = MainForm.CreateStatisticsPanel(
            valueLabels[0],
            valueLabels[1],
            valueLabels[2],
            valueLabels[3]);
        panel.Size = new Size(800, 58);
        panel.CreateControl();
        panel.PerformLayout();

        foreach (var item in panel.Controls.OfType<TableLayoutPanel>())
        {
            item.PerformLayout();
            Assert.Single(item.ColumnStyles);
            Assert.Equal(SizeType.Percent, item.ColumnStyles[0].SizeType);
        }

        Assert.Equal(4, panel.Controls.Count);
        Assert.Single(panel.RowStyles);
        Assert.Equal(SizeType.Percent, panel.RowStyles[0].SizeType);
        Assert.All(valueLabels, label =>
        {
            Assert.Equal("0", label.Text);
            Assert.True(label.Width > 0);
            Assert.True(label.Height >= 20);
        });
    }

    [Fact]
    public void CreateAssetDetailsPanel_ReservesReadablePreviewArea()
    {
        using var titleLabel = new Label();
        using var summaryLabel = new Label();
        using var previewTextBox = new TextBox();
        using var panel = MainForm.CreateAssetDetailsPanel(
            titleLabel,
            summaryLabel,
            previewTextBox);
        panel.Size = new Size(900, 150);
        panel.CreateControl();
        panel.PerformLayout();

        Assert.Equal(2, panel.ColumnStyles.Count);
        Assert.Equal(SizeType.Absolute, panel.ColumnStyles[0].SizeType);
        Assert.Equal(330, panel.ColumnStyles[0].Width);
        Assert.Equal(SizeType.Percent, panel.ColumnStyles[1].SizeType);
        Assert.True(previewTextBox.Width > 400);
        Assert.True(previewTextBox.Height >= 100);
        Assert.True(previewTextBox.Multiline);
        Assert.True(previewTextBox.ReadOnly);
    }
    [Fact]
    public void EnableAssetMultiSelection_AllowsFullRowBatchSelection()
    {
        using var grid = new DataGridView
        {
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };

        MainForm.EnableAssetMultiSelection(grid);

        Assert.True(grid.MultiSelect);
        Assert.Equal(
            DataGridViewSelectionMode.FullRowSelect,
            grid.SelectionMode);
    }

    [Fact]
    public void OpenFileLocationStartInfo_UsesExplorerWithStructuredArguments()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "Creator Assets", "clip.mp4");

        var startInfo = MainForm.CreateOpenFileLocationStartInfo(filePath);

        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(
            ["/select,", Path.GetFullPath(filePath)],
            startInfo.ArgumentList.ToArray());
    }

    [Fact]
    public void RightClickSelection_WithShift_SelectsTheAnchorRange()
    {
        using var grid = CreateSelectionGrid();
        grid.CurrentCell = grid.Rows[1].Cells[0];
        grid.ClearSelection();
        grid.Rows[1].Selected = true;

        MainForm.ApplyAssetGridRightClickSelection(
            grid,
            rowIndex: 4,
            columnIndex: 0,
            Keys.Shift);

        var selectedIndexes = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .Order()
            .ToArray();
        Assert.Equal([1, 2, 3, 4], selectedIndexes);
        Assert.Equal(4, grid.CurrentCell.RowIndex);
    }

    [Fact]
    public void RightClickSelection_PreservesBatchAndControlAddsARow()
    {
        using var grid = CreateSelectionGrid();
        grid.CurrentCell = grid.Rows[1].Cells[0];
        grid.ClearSelection();
        grid.Rows[1].Selected = true;
        grid.Rows[3].Selected = true;

        MainForm.ApplyAssetGridRightClickSelection(grid, 3, 0, Keys.None);
        Assert.Equal(
            [1, 3],
            grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.Index)
                .Order());

        MainForm.ApplyAssetGridRightClickSelection(grid, 4, 0, Keys.Control);
        Assert.Equal(
            [1, 3, 4],
            grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.Index)
                .Order());
    }

    private static DataGridView CreateSelectionGrid()
    {
        var grid = new DataGridView
        {
            AllowUserToAddRows = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.Columns.Add("Name", "Name");
        for (var index = 0; index < 5; index++)
        {
            grid.Rows.Add($"Asset {index}");
        }

        return grid;
    }

    [Fact]
    public void FileSizeColumn_SortsByRawBytesAcrossDisplayUnits()
    {
        using var grid = new DataGridView
        {
            AllowUserToAddRows = false
        };
        var column = MainForm.CreateFileSizeColumn();
        grid.Columns.Add(column);
        long[] sizes =
        [
            2L * 1024 * 1024 * 1024,
            950L * 1024 * 1024,
            10L * 1024,
            12L * 1024 * 1024 * 1024
        ];
        foreach (var size in sizes)
        {
            grid.Rows.Add(size);
        }

        grid.Sort(column, System.ComponentModel.ListSortDirection.Ascending);
        var ascending = grid.Rows
            .Cast<DataGridViewRow>()
            .Select(row => Assert.IsType<long>(row.Cells[0].Value))
            .ToArray();
        grid.Sort(column, System.ComponentModel.ListSortDirection.Descending);
        var descending = grid.Rows
            .Cast<DataGridViewRow>()
            .Select(row => Assert.IsType<long>(row.Cells[0].Value))
            .ToArray();

        Assert.Equal(typeof(long), column.ValueType);
        Assert.Equal(sizes.Order().ToArray(), ascending);
        Assert.Equal(sizes.OrderDescending().ToArray(), descending);
    }
}
