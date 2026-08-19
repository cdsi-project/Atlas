namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private static readonly int[] AssetPageSizes = [100, 200, 500];

    private readonly ComboBox _assetPageSizeComboBox = new();
    private readonly Button _previousAssetPageButton = new();
    private readonly Button _nextAssetPageButton = new();
    private readonly Label _assetPageLabel = new();
    private int _assetPageSize = AssetPageSizes[0];
    private long _assetPageIndex;
    private long _assetTotalItems;
    private bool _refreshingAssetPage;

    private TableLayoutPanel ConfigureAssetPagination()
    {
        var panel = CreateAssetPaginationPanel(
            _assetPageSizeComboBox,
            _previousAssetPageButton,
            _assetPageLabel,
            _nextAssetPageButton);
        _assetPageSizeComboBox.SelectedIndexChanged +=
            AssetPageSizeComboBox_SelectedIndexChanged;
        _previousAssetPageButton.Click += PreviousAssetPageButton_Click;
        _nextAssetPageButton.Click += NextAssetPageButton_Click;
        UpdateAssetPaginationControls(0);
        return panel;
    }

    internal static TableLayoutPanel CreateAssetPaginationPanel(
        ComboBox pageSizeComboBox,
        Button previousButton,
        Label pageLabel,
        Button nextButton)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = Padding.Empty,
            Padding = new Padding(8, 3, 8, 3),
            BackColor = Color.White
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pageSizePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        pageSizePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "每页",
            Margin = new Padding(0, 6, 8, 0),
            ForeColor = Color.FromArgb(88, 98, 106)
        });
        pageSizeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        pageSizeComboBox.Width = 76;
        pageSizeComboBox.Margin = Padding.Empty;
        pageSizeComboBox.AccessibleName = "每页资产数量";
        pageSizeComboBox.Items.AddRange([100, 200, 500]);
        pageSizeComboBox.SelectedIndex = 0;
        pageSizePanel.Controls.Add(pageSizeComboBox);

        pageLabel.Dock = DockStyle.Fill;
        pageLabel.Margin = Padding.Empty;
        pageLabel.Text = "第 1 / 1 页 · 0 条";
        pageLabel.TextAlign = ContentAlignment.MiddleCenter;
        pageLabel.ForeColor = Color.FromArgb(88, 98, 106);
        pageLabel.AccessibleName = "资产分页状态";

        ConfigurePageButton(previousButton, "上一页", "上一页");
        ConfigurePageButton(nextButton, "下一页", "下一页");
        var navigationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        navigationPanel.Controls.Add(previousButton);
        navigationPanel.Controls.Add(nextButton);

        panel.Controls.Add(pageSizePanel, 0, 0);
        panel.Controls.Add(pageLabel, 1, 0);
        panel.Controls.Add(navigationPanel, 2, 0);
        return panel;
    }

    internal static AssetPaginationState CalculateAssetPagination(
        long totalItems,
        int pageSize,
        long requestedPageIndex)
    {
        if (totalItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalItems));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var pageCount = totalItems == 0
            ? 1
            : ((totalItems - 1) / pageSize) + 1;
        var pageIndex = Math.Clamp(requestedPageIndex, 0, pageCount - 1);
        var offset = pageIndex * pageSize;
        var firstItem = totalItems == 0 ? 0 : offset + 1;
        var lastItem = Math.Min(totalItems, offset + pageSize);
        return new AssetPaginationState(
            pageIndex,
            pageCount,
            offset,
            firstItem,
            lastItem);
    }

    private static void ConfigurePageButton(
        Button button,
        string text,
        string accessibleName)
    {
        button.Text = text;
        button.AccessibleName = accessibleName;
        button.Size = new Size(76, 28);
        button.Margin = new Padding(4, 0, 0, 0);
        button.FlatStyle = FlatStyle.System;
    }

    private async void AssetPageSizeComboBox_SelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (_assetPageSizeComboBox.SelectedItem is not int selectedPageSize ||
            selectedPageSize == _assetPageSize)
        {
            return;
        }

        _assetPageSize = selectedPageSize;
        _assetPageIndex = 0;
        await RefreshAssetPageAsync();
    }

    private async void PreviousAssetPageButton_Click(
        object? sender,
        EventArgs e)
    {
        await NavigateAssetPageAsync(-1);
    }

    private async void NextAssetPageButton_Click(
        object? sender,
        EventArgs e)
    {
        await NavigateAssetPageAsync(1);
    }

    private async Task NavigateAssetPageAsync(int delta)
    {
        if (_refreshingAssetPage)
        {
            return;
        }

        var current = CalculateAssetPagination(
            _assetTotalItems,
            _assetPageSize,
            _assetPageIndex);
        var target = CalculateAssetPagination(
            _assetTotalItems,
            _assetPageSize,
            current.PageIndex + delta);
        if (target.PageIndex == current.PageIndex)
        {
            return;
        }

        _assetPageIndex = target.PageIndex;
        await RefreshAssetPageAsync();
    }

    private async Task RefreshAssetPageAsync()
    {
        if (_refreshingAssetPage)
        {
            return;
        }

        _refreshingAssetPage = true;
        UpdateAssetPaginationControls(_assetTotalItems);
        try
        {
            await RefreshAssetsAsync();
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "无法加载资产页";
            ShowError("无法加载资产页", exception);
        }
        finally
        {
            _refreshingAssetPage = false;
            UpdateAssetPaginationControls(_assetTotalItems);
        }
    }

    private void UpdateAssetPaginationControls(long totalItems)
    {
        var state = CalculateAssetPagination(
            totalItems,
            _assetPageSize,
            _assetPageIndex);
        _assetPageIndex = state.PageIndex;
        _assetTotalItems = totalItems;
        _assetPageLabel.Text = totalItems == 0
            ? "第 1 / 1 页 · 0 条"
            : $"第 {state.PageIndex + 1:N0} / {state.PageCount:N0} 页 · {state.FirstItem:N0}-{state.LastItem:N0} / {totalItems:N0}";

        var enabled = !_isBusy && !_refreshingAssetPage;
        _assetPageSizeComboBox.Enabled = enabled;
        _previousAssetPageButton.Enabled = enabled && state.PageIndex > 0;
        _nextAssetPageButton.Enabled =
            enabled && state.PageIndex + 1 < state.PageCount;
    }

    internal sealed record AssetPaginationState(
        long PageIndex,
        long PageCount,
        long Offset,
        long FirstItem,
        long LastItem);
}
