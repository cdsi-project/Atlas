using System.Drawing.Drawing2D;

namespace CDSI.Agent.WinForms;

internal sealed class AssetCompositionPieChart : Control
{
    private static readonly Color[] SliceColors =
    [
        Color.FromArgb(24, 121, 78),
        Color.FromArgb(194, 138, 33),
        Color.FromArgb(50, 116, 161),
        Color.FromArgb(184, 92, 75),
        Color.FromArgb(122, 133, 142)
    ];

    private AssetCompositionSlice[] _slices = CreateSlices(0, 0, 0, 0, 0);

    public AssetCompositionPieChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        MinimumSize = new Size(320, 210);
        AccessibleName = "资产类型分布饼图";
        AccessibleRole = AccessibleRole.Graphic;
        UpdateAccessibleDescription();
    }

    internal long TotalAssetCount { get; private set; }

    internal IReadOnlyList<AssetCompositionSlice> Slices => _slices;

    internal void SetValues(
        long totalAssetCount,
        long videoCount,
        long audioCount,
        long imageCount,
        long documentCount,
        long otherCount)
    {
        if (totalAssetCount < 0 ||
            videoCount < 0 ||
            audioCount < 0 ||
            imageCount < 0 ||
            documentCount < 0 ||
            otherCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalAssetCount),
                "资产统计不能为负数。");
        }

        TotalAssetCount = totalAssetCount;
        _slices = CreateSlices(
            videoCount,
            audioCount,
            imageCount,
            documentCount,
            otherCount);
        UpdateAccessibleDescription();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        var bounds = ClientRectangle;
        bounds.Inflate(-12, -10);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var headingFont = new Font(Font, FontStyle.Bold);
        TextRenderer.DrawText(
            e.Graphics,
            $"资产总数  {TotalAssetCount:N0}",
            headingFont,
            new Rectangle(bounds.Left, bounds.Top, bounds.Width, 24),
            Color.FromArgb(31, 37, 43),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);

        var contentBounds = new Rectangle(
            bounds.Left,
            bounds.Top + 32,
            bounds.Width,
            Math.Max(0, bounds.Height - 32));
        if (contentBounds.Height <= 0)
        {
            return;
        }

        var legendWidth = Math.Clamp(contentBounds.Width / 2, 150, 220);
        var pieAreaWidth = Math.Max(1, contentBounds.Width - legendWidth - 14);
        var pieSize = Math.Max(
            1,
            Math.Min(pieAreaWidth, contentBounds.Height) - 8);
        var pieBounds = new Rectangle(
            contentBounds.Left + Math.Max(0, (pieAreaWidth - pieSize) / 2),
            contentBounds.Top + Math.Max(0, (contentBounds.Height - pieSize) / 2),
            pieSize,
            pieSize);

        DrawPie(e.Graphics, pieBounds);
        DrawLegend(
            e.Graphics,
            new Rectangle(
                contentBounds.Right - legendWidth,
                contentBounds.Top,
                legendWidth,
                contentBounds.Height));
    }

    private void DrawPie(Graphics graphics, Rectangle bounds)
    {
        var total = _slices.Sum(slice => slice.Count);
        if (total == 0)
        {
            using var emptyBrush = new SolidBrush(Color.FromArgb(232, 235, 238));
            graphics.FillEllipse(emptyBrush, bounds);
            TextRenderer.DrawText(
                graphics,
                "暂无资产",
                Font,
                bounds,
                Color.FromArgb(112, 121, 129),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
        else
        {
            var startAngle = -90f;
            for (var index = 0; index < _slices.Length; index++)
            {
                var slice = _slices[index];
                if (slice.Count == 0)
                {
                    continue;
                }

                var sweepAngle = index == _slices.Length - 1
                    ? 270f - startAngle
                    : (float)(slice.Count * 360d / total);
                using var brush = new SolidBrush(slice.Color);
                graphics.FillPie(brush, bounds, startAngle, sweepAngle);
                startAngle += sweepAngle;
            }
        }

        using var borderPen = new Pen(Color.FromArgb(210, 215, 220));
        graphics.DrawEllipse(borderPen, bounds);
    }

    private void DrawLegend(Graphics graphics, Rectangle bounds)
    {
        var total = _slices.Sum(slice => slice.Count);
        var rowHeight = Math.Max(24, bounds.Height / _slices.Length);
        for (var index = 0; index < _slices.Length; index++)
        {
            var slice = _slices[index];
            var rowTop = bounds.Top + index * rowHeight;
            var swatchBounds = new Rectangle(
                bounds.Left,
                rowTop + Math.Max(0, (rowHeight - 12) / 2),
                12,
                12);
            using var brush = new SolidBrush(slice.Color);
            graphics.FillRectangle(brush, swatchBounds);

            var percentage = total == 0
                ? 0
                : slice.Count * 100d / total;
            TextRenderer.DrawText(
                graphics,
                $"{slice.Name}  {slice.Count:N0}  {percentage:N1}%",
                Font,
                new Rectangle(
                    swatchBounds.Right + 8,
                    rowTop,
                    Math.Max(0, bounds.Right - swatchBounds.Right - 8),
                    rowHeight),
                Color.FromArgb(52, 61, 69),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    private void UpdateAccessibleDescription()
    {
        AccessibleDescription = string.Join(
            "，",
            [$"资产总数 {TotalAssetCount:N0}", .. _slices.Select(
                slice => $"{slice.Name} {slice.Count:N0}")]);
    }

    private static AssetCompositionSlice[] CreateSlices(
        long videoCount,
        long audioCount,
        long imageCount,
        long documentCount,
        long otherCount)
    {
        return
        [
            new("视频", videoCount, SliceColors[0]),
            new("音频", audioCount, SliceColors[1]),
            new("图片", imageCount, SliceColors[2]),
            new("文本 / 文档", documentCount, SliceColors[3]),
            new("其他", otherCount, SliceColors[4])
        ];
    }
}

internal sealed record AssetCompositionSlice(
    string Name,
    long Count,
    Color Color);
