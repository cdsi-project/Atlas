using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    internal static readonly IReadOnlyList<AssetFileTypeFilterChoice>
        AssetFileTypeFilterChoices =
        [
            new(AssetFileTypeFilter.All, "全部类型"),
            new(AssetFileTypeFilter.Video, "视频"),
            new(AssetFileTypeFilter.Audio, "音频"),
            new(AssetFileTypeFilter.Image, "图片"),
            new(AssetFileTypeFilter.Document, "文档"),
            new(AssetFileTypeFilter.Other, "其他")
        ];

    private readonly ComboBox _assetFileTypeFilterComboBox = new();
    private readonly DateTimePicker _assetCreatedFromDatePicker = new();
    private readonly DateTimePicker _assetCreatedToDatePicker = new();
    private readonly Button _applyAssetFilterButton = new();
    private readonly Button _resetAssetFilterButton = new();
    private readonly Label _assetFilterResultLabel = new();
    private AssetListFilter _assetListFilter = AssetListFilter.Empty;

    private Control ConfigureAssetFilterPanel()
    {
        _assetFileTypeFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _assetFileTypeFilterComboBox.Width = 108;
        _assetFileTypeFilterComboBox.AccessibleName = "文件类型过滤";
        _assetFileTypeFilterComboBox.Items.AddRange(
            AssetFileTypeFilterChoices.Cast<object>().ToArray());
        _assetFileTypeFilterComboBox.SelectedIndex = 0;

        ConfigureFilterDatePicker(
            _assetCreatedFromDatePicker,
            "创建时间开始",
            DateTime.Today.AddMonths(-1));
        ConfigureFilterDatePicker(
            _assetCreatedToDatePicker,
            "创建时间结束",
            DateTime.Today);

        ConfigureFilterButton(
            _applyAssetFilterButton,
            "应用",
            Color.FromArgb(24, 121, 78),
            Color.White);
        ConfigureFilterButton(
            _resetAssetFilterButton,
            "重置",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));
        _applyAssetFilterButton.Click += async (_, _) =>
            await ApplyAssetFilterAsync();
        _resetAssetFilterButton.Click += async (_, _) =>
            await ResetAssetFilterAsync();

        _assetFilterResultLabel.AutoSize = true;
        _assetFilterResultLabel.Text = "全部资产";
        _assetFilterResultLabel.Margin = new Padding(10, 8, 0, 0);
        _assetFilterResultLabel.ForeColor = Color.FromArgb(88, 98, 106);
        _assetFilterResultLabel.AccessibleName = "资产过滤结果";

        return CreateAssetFilterPanel(
            _assetFileTypeFilterComboBox,
            _assetCreatedFromDatePicker,
            _assetCreatedToDatePicker,
            _applyAssetFilterButton,
            _resetAssetFilterButton,
            _assetFilterResultLabel);
    }

    internal static Control CreateAssetFilterPanel(
        ComboBox fileTypeComboBox,
        DateTimePicker createdFromDatePicker,
        DateTimePicker createdToDatePicker,
        Button applyButton,
        Button resetButton,
        Label resultLabel)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(8, 8, 8, 6),
            BackColor = Color.FromArgb(247, 248, 250)
        };
        panel.Controls.Add(CreateFilterLabel("文件类型"));
        panel.Controls.Add(fileTypeComboBox);
        panel.Controls.Add(CreateFilterLabel("创建时间", leftMargin: 14));
        panel.Controls.Add(createdFromDatePicker);
        panel.Controls.Add(CreateFilterLabel("至", leftMargin: 6));
        panel.Controls.Add(createdToDatePicker);
        panel.Controls.Add(applyButton);
        panel.Controls.Add(resetButton);
        panel.Controls.Add(resultLabel);
        return panel;
    }

    internal static AssetListFilter BuildAssetListFilter(
        AssetFileTypeFilter fileType,
        bool createdFromEnabled,
        DateTime createdFrom,
        bool createdToEnabled,
        DateTime createdTo)
    {
        if (createdFromEnabled &&
            createdToEnabled &&
            createdFrom.Date > createdTo.Date)
        {
            throw new ArgumentException("创建时间的开始日期不能晚于结束日期。");
        }

        DateTimeOffset? createdFromBoundary = createdFromEnabled
            ? StartOfLocalDay(createdFrom)
            : null;
        DateTimeOffset? createdBeforeBoundary = createdToEnabled
            ? StartOfLocalDay(createdTo.Date.AddDays(1))
            : null;
        return new AssetListFilter(
            fileType,
            createdFromBoundary,
            createdBeforeBoundary);
    }

    private static DateTimeOffset StartOfLocalDay(DateTime value)
    {
        var localDate = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
        var utcDate = TimeZoneInfo.ConvertTimeToUtc(localDate, TimeZoneInfo.Local);
        return new DateTimeOffset(utcDate, TimeSpan.Zero);
    }

    private static Label CreateFilterLabel(string text, int leftMargin = 0)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Margin = new Padding(leftMargin, 7, 6, 0),
            ForeColor = Color.FromArgb(52, 61, 69)
        };
    }

    private static void ConfigureFilterDatePicker(
        DateTimePicker datePicker,
        string accessibleName,
        DateTime value)
    {
        datePicker.Format = DateTimePickerFormat.Custom;
        datePicker.CustomFormat = "yyyy-MM-dd";
        datePicker.ShowCheckBox = true;
        datePicker.Checked = false;
        datePicker.Value = value;
        datePicker.Width = 126;
        datePicker.AccessibleName = accessibleName;
    }

    private static void ConfigureFilterButton(
        Button button,
        string text,
        Color background,
        Color foreground)
    {
        button.Text = text;
        button.Size = new Size(72, 28);
        button.Margin = new Padding(8, 0, 0, 0);
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    private async Task ApplyAssetFilterAsync()
    {
        if (_assetFileTypeFilterComboBox.SelectedItem is not
            AssetFileTypeFilterChoice selectedType)
        {
            return;
        }

        try
        {
            _assetListFilter = BuildAssetListFilter(
                selectedType.Value,
                _assetCreatedFromDatePicker.Checked,
                _assetCreatedFromDatePicker.Value,
                _assetCreatedToDatePicker.Checked,
                _assetCreatedToDatePicker.Value);
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _assetPageIndex = 0;
        await RefreshAssetPageAsync();
    }

    private async Task ResetAssetFilterAsync()
    {
        _assetFileTypeFilterComboBox.SelectedIndex = 0;
        _assetCreatedFromDatePicker.Checked = false;
        _assetCreatedToDatePicker.Checked = false;
        _assetListFilter = AssetListFilter.Empty;
        _assetPageIndex = 0;
        await RefreshAssetPageAsync();
    }

    private void UpdateAssetFilterResult(long filteredCount, long totalCount)
    {
        _assetFilterResultLabel.Text = _assetListFilter.IsEmpty
            ? $"全部 {totalCount:N0}"
            : $"筛选结果 {filteredCount:N0} / {totalCount:N0}";
    }

    private void UpdateAssetFilterControlState()
    {
        var enabled = !_isBusy && !_refreshingAssetPage;
        _assetFileTypeFilterComboBox.Enabled = enabled;
        _assetCreatedFromDatePicker.Enabled = enabled;
        _assetCreatedToDatePicker.Enabled = enabled;
        _applyAssetFilterButton.Enabled = enabled;
        _resetAssetFilterButton.Enabled = enabled;
    }

    internal sealed record AssetFileTypeFilterChoice(
        AssetFileTypeFilter Value,
        string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
