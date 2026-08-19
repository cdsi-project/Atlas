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
