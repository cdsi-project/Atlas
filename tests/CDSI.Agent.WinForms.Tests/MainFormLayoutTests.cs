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
    public void CreateAssetDetailsPanel_UsesTheFullAvailableWidth()
    {
        using var titleLabel = new Label();
        using var summaryLabel = new Label();
        using var panel = MainForm.CreateAssetDetailsPanel(
            titleLabel,
            summaryLabel);
        panel.Size = new Size(900, 150);
        panel.CreateControl();
        panel.PerformLayout();
        var summaryPanel = Assert.Single(
            panel.Controls.OfType<TableLayoutPanel>());
        summaryPanel.PerformLayout();

        Assert.Single(panel.ColumnStyles);
        Assert.Equal(SizeType.Percent, panel.ColumnStyles[0].SizeType);
        Assert.Equal(2, panel.Controls.Count);
        Assert.Equal(
            "资产详情",
            Assert.Single(panel.Controls.OfType<Label>()).Text);
        Assert.True(titleLabel.Width > 800);
        Assert.True(summaryLabel.Width > 800);
        Assert.DoesNotContain(
            panel.Controls.Cast<Control>(),
            control => control is TextBox);
    }
    [Fact]
    public void CreateAssetPaginationPanel_OffersSupportedPageSizes()
    {
        using var pageSizeComboBox = new ComboBox();
        using var previousButton = new Button();
        using var pageLabel = new Label();
        using var nextButton = new Button();
        using var panel = MainForm.CreateAssetPaginationPanel(
            pageSizeComboBox,
            previousButton,
            pageLabel,
            nextButton);
        panel.Size = new Size(900, 36);
        panel.CreateControl();
        panel.PerformLayout();

        Assert.Equal(
            [100, 200, 500],
            pageSizeComboBox.Items.Cast<int>());
        Assert.Equal(ComboBoxStyle.DropDownList, pageSizeComboBox.DropDownStyle);
        Assert.Equal(100, pageSizeComboBox.SelectedItem);
        Assert.Equal("上一页", previousButton.Text);
        Assert.Equal("下一页", nextButton.Text);
        Assert.Equal("第 1 / 1 页 · 0 条", pageLabel.Text);
    }

    [Fact]
    public void CalculateAssetPagination_ClampsToTheLastAvailablePage()
    {
        var state = MainForm.CalculateAssetPagination(
            totalItems: 250,
            pageSize: 100,
            requestedPageIndex: 99);

        Assert.Equal(2, state.PageIndex);
        Assert.Equal(3, state.PageCount);
        Assert.Equal(200, state.Offset);
        Assert.Equal(201, state.FirstItem);
        Assert.Equal(250, state.LastItem);
    }

    [Fact]
    public void CalculateAssetPagination_RepresentsAnEmptyListAsPageOne()
    {
        var state = MainForm.CalculateAssetPagination(
            totalItems: 0,
            pageSize: 200,
            requestedPageIndex: 5);

        Assert.Equal(0, state.PageIndex);
        Assert.Equal(1, state.PageCount);
        Assert.Equal(0, state.Offset);
        Assert.Equal(0, state.FirstItem);
        Assert.Equal(0, state.LastItem);
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

    [Fact]
    public void AssetIdColumn_DisplaysTheStableAssetIdentifier()
    {
        var assetId = Guid.Parse("6a85382d-fdfd-4533-ad6f-14333ad6f14a");
        using var grid = new DataGridView
        {
            AllowUserToAddRows = false
        };
        var column = MainForm.CreateAssetIdColumn();
        grid.Columns.Add(column);
        grid.Rows.Add(assetId.ToString("D"));

        Assert.Equal("AssetId", column.Name);
        Assert.Equal("资产 ID", column.HeaderText);
        Assert.Equal(typeof(string), column.ValueType);
        Assert.Equal(
            "6a85382d-fdfd-4533-ad6f-14333ad6f14a",
            grid.Rows[0].Cells[0].Value);
    }
}
