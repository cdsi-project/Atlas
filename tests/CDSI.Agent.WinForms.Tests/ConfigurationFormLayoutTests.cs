using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Storage;
using CDSI.Agent.Application.Workspaces;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Persistence;
using CDSI.Agent.Infrastructure.Security;
using CDSI.Agent.WinForms;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms.Tests;

public sealed class ConfigurationFormLayoutTests
{
    [Fact]
    public void FirstRunSetupForm_ProvidesAStableWorkspacePathControl()
    {
        using var form = new FirstRunSetupForm();
        form.CreateControl();

        var pathTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control => control.AccessibleName == "工作目录路径");

        Assert.False(string.IsNullOrWhiteSpace(form.SelectedPath));
        Assert.Equal(DockStyle.Fill, pathTextBox.Dock);
        Assert.True(form.ClientSize.Width >= 560);
    }

    [Fact]
    public void SettingsForm_SeparatesWorkspaceAndExternalScanRoots()
    {
        var repository = new SqliteAssetRepository(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db"));
        using var form = new SettingsForm(
            new WorkspaceApplicationService(
                repository,
                new WorkspaceProvisioner()),
            new ScanRootManagementService(repository),
            new ObjectStorageProfileService(
                repository,
                new WindowsCredentialSecretStore()),
            new OpenWebSettingsService(
                repository,
                new WindowsCredentialSecretStore()));
        form.CreateControl();

        var tabs = Assert.Single(Descendants(form).OfType<TabControl>());
        var rootsGrid = Descendants(form)
            .OfType<DataGridView>()
            .Single(grid => grid.AccessibleName == "外部扫描目录列表");

        var storageGrid = Descendants(form)
            .OfType<DataGridView>()
            .Single(grid => grid.AccessibleName == "OSS 配置列表");

        var openWebOriginDomainTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control =>
                control.AccessibleName == "OpenWeb 源站域名");
        var openWebUsernameTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control => control.AccessibleName == "WordPress 用户名");
        var openWebPasswordTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control =>
                control.AccessibleName == "WordPress 应用程序密码");
        var startScanButton = Descendants(form)
            .OfType<Button>()
            .Single(button => button.Text == "开始扫描");
        var closeButton = Descendants(form)
            .OfType<Button>()
            .Single(button => button.Text == "关闭");

        Assert.Equal(4, tabs.TabPages.Count);
        Assert.Equal("工作目录", tabs.TabPages[0].Text);
        Assert.Equal("扫描目录", tabs.TabPages[1].Text);
        Assert.Equal("OSS 配置", tabs.TabPages[2].Text);
        Assert.Equal("OpenWeb", tabs.TabPages[3].Text);
        Assert.Equal(DockStyle.Fill, openWebOriginDomainTextBox.Dock);
        Assert.Equal(DockStyle.Fill, openWebUsernameTextBox.Dock);
        Assert.True(openWebPasswordTextBox.UseSystemPasswordChar);
        Assert.Equal(4, rootsGrid.Columns.Count);
        Assert.Equal(
            ["目录", "文件类型", "状态", "最近扫描"],
            rootsGrid.Columns
                .Cast<DataGridViewColumn>()
                .Select(column => column.HeaderText)
                .ToArray());
        Assert.Equal(DataGridViewAutoSizeColumnMode.Fill, rootsGrid.Columns[0].AutoSizeMode);
        Assert.True(rootsGrid.Columns[0].MinimumWidth >= 320);
        Assert.Equal(5, storageGrid.Columns.Count);
        Assert.False(startScanButton.Enabled);
        Assert.Equal(DialogResult.OK, startScanButton.DialogResult);
        Assert.Equal(DialogResult.Cancel, closeButton.DialogResult);
    }

    [Fact]
    public void ScanRootDialog_DefaultsToAllFileTypesWithoutOverlappingControls()
    {
        using var dialog = new ScanRootDialog(Path.GetTempPath());
        dialog.CreateControl();
        dialog.PerformLayout();
        var fileTypeComboBox = Descendants(dialog)
            .OfType<ComboBox>()
            .Single(control => control.AccessibleName == "扫描文件类型");
        var fileTypeLabel = Descendants(dialog)
            .OfType<Label>()
            .Single(label => label.Text == "文件类型");

        Assert.Equal(AssetFileTypeFilter.All, dialog.FileTypeFilter);
        Assert.Equal("全部类型", fileTypeComboBox.SelectedItem?.ToString());
        Assert.Equal(6, fileTypeComboBox.Items.Count);
        Assert.False(fileTypeLabel.Bounds.IntersectsWith(fileTypeComboBox.Bounds));
    }

    [Fact]
    public void OssProfileDialog_NeverPrefillsOrRevealsTheStoredSecret()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = new ObjectStorageProfile(
            Guid.NewGuid(),
            "主 OSS",
            ObjectStorageProvider.AliyunOss,
            "oss-cn-hangzhou.aliyuncs.com",
            "cdsi-assets",
            "cn-hangzhou",
            true,
            "access-key-id",
            now,
            now);
        using var form = new OssProfileDialog(profile);
        form.CreateControl();

        var secretTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control => control.AccessibleName == "AccessKey Secret");

        Assert.True(secretTextBox.UseSystemPasswordChar);
        Assert.Empty(secretTextBox.Text);
        Assert.Null(form.CreateRequest().AccessKeySecret);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
