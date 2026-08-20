using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private readonly TabPage _statisticsTabPage = new("统计");
    private readonly Label _statisticsAssetCountValueLabel = new();
    private readonly Label _statisticsVideoCountValueLabel = new();
    private readonly Label _statisticsAudioCountValueLabel = new();
    private readonly Label _statisticsImageCountValueLabel = new();
    private readonly Label _statisticsDocumentCountValueLabel = new();
    private readonly Label _statisticsOtherCountValueLabel = new();
    private readonly Label _statisticsLocalFileCountValueLabel = new();
    private readonly Label _statisticsUnavailableCountValueLabel = new();
    private readonly Label _statisticsBackedUpCountValueLabel = new();
    private readonly Label _statisticsUnbackedUpCountValueLabel = new();
    private readonly Label _statisticsStorageValueLabel = new();
    private readonly Label _statisticsBackupCoverageValueLabel = new();
    private readonly Label _statisticsVideoDurationValueLabel = new();

    private void ConfigureStatisticsTab()
    {
        _statisticsTabPage.Padding = Padding.Empty;
        _statisticsTabPage.BackColor = Color.White;
        _statisticsTabPage.Controls.Add(CreateStatisticsDashboard(
            [
                new StatisticsSection(
                    "资产构成",
                    [
                        new("资产总数", _statisticsAssetCountValueLabel),
                        new("视频文件", _statisticsVideoCountValueLabel),
                        new("音频文件", _statisticsAudioCountValueLabel),
                        new("图片文件", _statisticsImageCountValueLabel),
                        new("文本 / 文档", _statisticsDocumentCountValueLabel),
                        new("其他类型", _statisticsOtherCountValueLabel)
                    ]),
                new StatisticsSection(
                    "存储与保护",
                    [
                        new("可用本地文件", _statisticsLocalFileCountValueLabel),
                        new("不可用资产", _statisticsUnavailableCountValueLabel),
                        new("已备份资产", _statisticsBackedUpCountValueLabel),
                        new("未备份资产", _statisticsUnbackedUpCountValueLabel),
                        new("占用存储空间", _statisticsStorageValueLabel),
                        new("备份覆盖率", _statisticsBackupCoverageValueLabel)
                    ]),
                new StatisticsSection(
                    "媒体",
                    [
                        new("视频总时长", _statisticsVideoDurationValueLabel)
                    ])
            ]));
    }

    internal static TableLayoutPanel CreateStatisticsDashboard(
        IReadOnlyList<StatisticsSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var dashboard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = sections.Count * 2 + 1,
            Margin = Padding.Empty,
            Padding = new Padding(20, 16, 20, 20),
            BackColor = Color.White
        };
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        foreach (var section in sections)
        {
            dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            dashboard.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Text = section.Title,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Color.FromArgb(52, 61, 69)
            }, 0, row++);

            var metricRows = Math.Max(1, (int)Math.Ceiling(section.Metrics.Count / 3d));
            dashboard.RowStyles.Add(new RowStyle(
                SizeType.Absolute,
                metricRows * 76));
            dashboard.Controls.Add(CreateStatisticsMetricGrid(section.Metrics), 0, row++);
        }

        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return dashboard;
    }

    private static TableLayoutPanel CreateStatisticsMetricGrid(
        IReadOnlyList<StatisticsMetric> metrics)
    {
        var rows = Math.Max(1, (int)Math.Ceiling(metrics.Count / 3d));
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = rows,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            BackColor = Color.FromArgb(224, 228, 232)
        };
        for (var column = 0; column < grid.ColumnCount; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        }

        for (var row = 0; row < rows; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        }

        for (var index = 0; index < metrics.Count; index++)
        {
            var metric = metrics[index];
            grid.Controls.Add(
                CreateDashboardMetric(metric.Title, metric.ValueLabel),
                index % 3,
                index / 3);
        }

        return grid;
    }

    private static Control CreateDashboardMetric(string title, Label valueLabel)
    {
        var item = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(14, 9, 14, 8),
            BackColor = Color.White
        };
        item.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        item.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        item.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        item.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(88, 98, 106)
        }, 0, 0);

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Margin = Padding.Empty;
        valueLabel.Text = "0";
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.AutoEllipsis = true;
        valueLabel.Font = new Font("Segoe UI Semibold", 15F);
        valueLabel.ForeColor = Color.FromArgb(31, 37, 43);
        valueLabel.AccessibleName = title;
        item.Controls.Add(valueLabel, 0, 1);
        return item;
    }

    private void UpdateStatisticsDashboard(AssetStatistics statistics)
    {
        _statisticsAssetCountValueLabel.Text = statistics.AssetCount.ToString("N0");
        _statisticsVideoCountValueLabel.Text = statistics.VideoAssetCount.ToString("N0");
        _statisticsAudioCountValueLabel.Text = statistics.AudioAssetCount.ToString("N0");
        _statisticsImageCountValueLabel.Text = statistics.ImageAssetCount.ToString("N0");
        _statisticsDocumentCountValueLabel.Text = statistics.DocumentAssetCount.ToString("N0");
        _statisticsOtherCountValueLabel.Text = statistics.OtherAssetCount.ToString("N0");
        _statisticsLocalFileCountValueLabel.Text =
            statistics.AvailableLocalFileCount.ToString("N0");
        _statisticsUnavailableCountValueLabel.Text =
            statistics.UnavailableAssetCount.ToString("N0");
        _statisticsBackedUpCountValueLabel.Text =
            statistics.BackedUpAssetCount.ToString("N0");
        _statisticsUnbackedUpCountValueLabel.Text =
            statistics.UnbackedUpAssetCount.ToString("N0");
        _statisticsStorageValueLabel.Text = FormatFileSize(statistics.TotalSizeBytes);
        _statisticsBackupCoverageValueLabel.Text = statistics.AssetCount == 0
            ? "0.0%"
            : ((double)statistics.BackedUpAssetCount / statistics.AssetCount)
                .ToString("P1");
        _statisticsVideoDurationValueLabel.Text =
            FormatTotalDuration(statistics.VideoDurationMilliseconds);
    }

    internal sealed record StatisticsSection(
        string Title,
        IReadOnlyList<StatisticsMetric> Metrics);

    internal sealed record StatisticsMetric(
        string Title,
        Label ValueLabel);
}
