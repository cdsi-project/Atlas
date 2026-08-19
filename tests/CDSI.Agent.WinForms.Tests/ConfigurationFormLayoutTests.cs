using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Workspaces;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Persistence;
using CDSI.Agent.WinForms;

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
            new ScanRootManagementService(repository));
        form.CreateControl();

        var tabs = Assert.Single(Descendants(form).OfType<TabControl>());
        var rootsGrid = Descendants(form)
            .OfType<DataGridView>()
            .Single(grid => grid.AccessibleName == "外部扫描目录列表");

        Assert.Equal(2, tabs.TabPages.Count);
        Assert.Equal("工作目录", tabs.TabPages[0].Text);
        Assert.Equal("扫描目录", tabs.TabPages[1].Text);
        Assert.Equal(3, rootsGrid.Columns.Count);
        Assert.Equal(DataGridViewAutoSizeColumnMode.Fill, rootsGrid.Columns[0].AutoSizeMode);
        Assert.True(rootsGrid.Columns[0].MinimumWidth >= 320);
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
