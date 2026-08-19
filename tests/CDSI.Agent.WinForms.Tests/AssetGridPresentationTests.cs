using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class AssetGridPresentationTests
{
    [Fact]
    public void GridCellFormatting_UsesGreenForHealthyOssBackups()
    {
        using var grid = new DataGridView();
        grid.Columns.Add(MainForm.CreateObjectStorageStatusColumn());
        var style = new DataGridViewCellStyle();
        var args = new DataGridViewCellFormattingEventArgs(
            0,
            0,
            "已备份",
            typeof(string),
            style);

        MainForm.Grid_CellFormatting(grid, args);

        var expected = Color.FromArgb(24, 121, 78);
        Assert.Equal(expected, style.ForeColor);
        Assert.Equal(expected, style.SelectionForeColor);
    }

    [Fact]
    public void TransferSpeedTracker_UsesTransferredByteDeltas()
    {
        var tracker = new TransferSpeedTracker(timestampFrequency: 1_000);

        Assert.Equal(0, tracker.Update(0, timestamp: 0));
        Assert.Equal(
            4d * 1024 * 1024,
            tracker.Update(1L * 1024 * 1024, timestamp: 250));
        Assert.Equal(
            2d * 1024 * 1024,
            tracker.Update(3L * 1024 * 1024, timestamp: 1_250));
        Assert.Equal(
            0,
            tracker.Update(3L * 1024 * 1024, timestamp: 2_250));

        tracker.Reset();
        Assert.Equal(0, tracker.BytesPerSecond);
    }
}
