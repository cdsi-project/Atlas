using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.WinForms;

public sealed partial class SettingsForm
{
    private readonly OpenWebSettingsService _openWebSettingsService;
    private readonly TextBox _openWebOriginDomainTextBox = new();
    private readonly Label _openWebStatusLabel = new();

    private TabPage CreateOpenWebPage()
    {
        var page = new TabPage("OpenWeb")
        {
            BackColor = Color.White,
            Padding = new Padding(24)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 170,
            ColumnCount = 2,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var titleLabel = new Label
        {
            Text = "OpenWeb",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(31, 37, 43)
        };
        layout.Controls.Add(titleLabel, 0, 0);
        layout.SetColumnSpan(titleLabel, 2);

        var domainLabel = new Label
        {
            Text = "源站域名",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(72, 81, 89)
        };
        layout.Controls.Add(domainLabel, 0, 1);
        layout.SetColumnSpan(domainLabel, 2);

        _openWebOriginDomainTextBox.Dock = DockStyle.Fill;
        _openWebOriginDomainTextBox.Margin = new Padding(0, 5, 8, 5);
        _openWebOriginDomainTextBox.AccessibleName = "OpenWeb 源站域名";
        layout.Controls.Add(_openWebOriginDomainTextBox, 0, 2);

        var saveButton = CreateButton(
            "应用",
            Color.FromArgb(24, 121, 78),
            Color.White);
        saveButton.Margin = new Padding(4, 5, 0, 5);
        saveButton.AccessibleName = "保存 OpenWeb 设置";
        saveButton.Click += OpenWebSaveButton_Click;
        layout.Controls.Add(saveButton, 1, 2);

        _openWebStatusLabel.Dock = DockStyle.Fill;
        _openWebStatusLabel.ForeColor = Color.FromArgb(88, 98, 106);
        _openWebStatusLabel.Padding = new Padding(0, 8, 0, 0);
        _openWebStatusLabel.AccessibleName = "OpenWeb 设置状态";
        layout.Controls.Add(_openWebStatusLabel, 0, 3);
        layout.SetColumnSpan(_openWebStatusLabel, 2);

        page.Controls.Add(layout);
        return page;
    }

    private async Task RefreshOpenWebAsync()
    {
        var settings = await _openWebSettingsService.GetAsync();
        _openWebOriginDomainTextBox.Text = settings.OriginDomain ?? string.Empty;
        _openWebStatusLabel.Text = FormatOpenWebStatus(settings);
    }

    private async void OpenWebSaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var settings = await _openWebSettingsService.SaveAsync(
                _openWebOriginDomainTextBox.Text);
            _openWebOriginDomainTextBox.Text =
                settings.OriginDomain ?? string.Empty;
            _openWebStatusLabel.Text = FormatOpenWebStatus(settings);
        }
        catch (Exception exception)
        {
            ShowError("无法保存 OpenWeb 设置", exception);
        }
    }

    private static string FormatOpenWebStatus(OpenWebSettings settings)
    {
        if (settings.OriginDomain is null)
        {
            return "未配置";
        }

        return settings.UpdatedAt is null
            ? "已保存"
            : $"已保存 · {settings.UpdatedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
    }
}
