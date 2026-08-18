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
}
