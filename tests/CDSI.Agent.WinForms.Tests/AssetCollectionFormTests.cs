using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Collections;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class AssetCollectionFormTests
{
    [Fact]
    public void CreateDialog_OffersAllFiveCollectionTypes()
    {
        using var form = new AssetCollectionDialog();

        Assert.Equal(5, AssetCollectionDialog.CollectionTypeChoices.Count);
        Assert.Equal(
            Enum.GetValues<AssetCollectionType>(),
            AssetCollectionDialog.CollectionTypeChoices.Select(choice => choice.Type));
        Assert.Equal(AssetCollectionType.Mixed, form.CollectionType);
    }

    [Fact]
    public void CreateDialog_OffersConfiguredCloudBackupProvidersAndNoBinding()
    {
        var profiles = new[]
        {
            CreateBackupProfile("阿里主存储", ObjectStorageProvider.AliyunOss),
            CreateBackupProfile("腾讯归档", ObjectStorageProvider.TencentCos),
            CreateBackupProfile("七牛分发", ObjectStorageProvider.QiniuKodo)
        };
        using var form = new AssetCollectionDialog(profiles);
        form.CreateControl();
        var backupComboBox = Assert.Single(Descendants(form).OfType<ComboBox>(),
            comboBox => comboBox.AccessibleName == "云端备份");

        Assert.Equal(4, backupComboBox.Items.Count);
        Assert.Equal("暂不绑定", backupComboBox.GetItemText(backupComboBox.Items[0]));
        Assert.Contains("阿里云 OSS · 阿里主存储", backupComboBox.GetItemText(backupComboBox.Items[1]));
        Assert.Contains("腾讯云 COS · 腾讯归档", backupComboBox.GetItemText(backupComboBox.Items[2]));
        Assert.Contains("七牛云 Kodo · 七牛分发", backupComboBox.GetItemText(backupComboBox.Items[3]));
        Assert.Null(form.BackupProfileId);

        backupComboBox.SelectedIndex = 2;
        Assert.Equal(profiles[1].Profile.Id, form.BackupProfileId);
    }

    [Fact]
    public void ProjectBackupBinding_RestrictsSyncToTheBoundProfile()
    {
        var profiles = new[]
        {
            CreateBackupProfile("阿里主存储", ObjectStorageProvider.AliyunOss),
            CreateBackupProfile("腾讯归档", ObjectStorageProvider.TencentCos),
            CreateBackupProfile("七牛分发", ObjectStorageProvider.QiniuKodo)
        };

        var selected = MainForm.SelectBackupProfiles(
            profiles,
            profiles[1].Profile.Id);

        Assert.Equal(profiles[1].Profile.Id, Assert.Single(selected).Profile.Id);
        Assert.Equal(3, MainForm.SelectBackupProfiles(profiles, null).Count);
    }

    [Fact]
    public void CollectionLayout_KeepsListsInSeparateResizablePanes()
    {
        using var collectionGrid = new DataGridView();
        using var memberGrid = new DataGridView();
        using var createButton = new Button { Text = "新建项目" };
        using var syncButton = new Button { Text = "同步到 OSS" };
        using var layout = MainForm.CreateAssetCollectionLayout(
            collectionGrid,
            memberGrid,
            createButton,
            syncButton);
        layout.Size = new Size(1100, 520);
        layout.CreateControl();
        layout.PerformLayout();

        var split = Assert.Single(Descendants(layout).OfType<SplitContainer>());
        Assert.Equal(Orientation.Vertical, split.Orientation);
        Assert.True(split.Panel1MinSize >= 300);
        Assert.True(split.Panel2MinSize >= 420);
        Assert.Contains(collectionGrid, Descendants(split.Panel1));
        Assert.Contains(memberGrid, Descendants(split.Panel2));
        Assert.Contains(
            Descendants(split.Panel1).OfType<Label>(),
            label => label.Text == "项目列表");
        Assert.Contains(
            Descendants(split.Panel2).OfType<Label>(),
            label => label.Text == "项目内资产");
        var toolbar = Assert.Single(
            layout.Controls.OfType<FlowLayoutPanel>());
        Assert.Equal(
            ["新建项目", "同步到 OSS"],
            toolbar.Controls.OfType<Button>().Select(button => button.Text));
    }

    [Fact]
    public void ProjectContextMenu_OffersSyncAndDeleteCommands()
    {
        using var contextMenu = new ContextMenuStrip();
        using var syncItem = new ToolStripMenuItem();
        using var deleteItem = new ToolStripMenuItem();

        MainForm.ConfigureProjectContextMenu(
            contextMenu,
            syncItem,
            deleteItem);

        Assert.Equal(3, contextMenu.Items.Count);
        Assert.Same(syncItem, contextMenu.Items[0]);
        Assert.Equal("同步到 OSS", syncItem.Text);
        Assert.IsType<ToolStripSeparator>(contextMenu.Items[1]);
        Assert.Same(deleteItem, contextMenu.Items[2]);
        Assert.Equal("删除项目", deleteItem.Text);
    }

    [Fact]
    public void AddToProjectMenu_ShowsThreeProjectsThenMore()
    {
        var projects = Enumerable.Range(1, 4)
            .Select(index => CreateProject($"项目 {index}"))
            .ToArray();
        using var menuItem = new ToolStripMenuItem();

        MainForm.PopulateAddToProjectMenu(menuItem, projects, selectedAssetCount: 2);

        Assert.Equal("加入项目 (2)", menuItem.Text);
        Assert.True(menuItem.Enabled);
        Assert.Collection(
            menuItem.DropDownItems.Cast<ToolStripItem>(),
            item => Assert.Equal("新建项目", item.Text),
            item => Assert.IsType<ToolStripSeparator>(item),
            item => Assert.Equal(projects[0].Id, item.Tag),
            item => Assert.Equal(projects[1].Id, item.Tag),
            item => Assert.Equal(projects[2].Id, item.Tag),
            item => Assert.IsType<ToolStripSeparator>(item),
            item => Assert.Equal("更多...", item.Text));

        MainForm.PopulateAddToProjectMenu(
            menuItem,
            projects.Take(3).ToArray(),
            selectedAssetCount: 1);

        Assert.Equal(5, menuItem.DropDownItems.Count);
        Assert.DoesNotContain(
            menuItem.DropDownItems.Cast<ToolStripItem>(),
            item => item.Text == "更多...");
    }

    [Fact]
    public void AddToProjectMenu_OffersProjectCreationWhenEmpty()
    {
        using var menuItem = new ToolStripMenuItem();

        MainForm.PopulateAddToProjectMenu(
            menuItem,
            [],
            selectedAssetCount: 1);

        var createItem = Assert.Single(
            menuItem.DropDownItems.Cast<ToolStripItem>());
        Assert.Equal("新建项目", createItem.Text);
    }

    [Fact]
    public void SyncToProjectMenu_ListsOnlyCommonProjectsAndThenMore()
    {
        var projects = Enumerable.Range(1, 4)
            .Select(index => CreateProject($"项目 {index}"))
            .ToArray();
        var firstAsset = CreateAsset(
            "first.mp4",
            projects.Select(project => project.Name).ToArray());
        var secondAsset = CreateAsset(
            "second.mp4",
            projects.Select(project => project.Name).ToArray());
        var commonProjects = MainForm.FindCommonProjects(
            projects,
            [firstAsset, secondAsset]);
        using var menuItem = new ToolStripMenuItem();

        MainForm.PopulateSyncToProjectMenu(
            menuItem,
            commonProjects,
            selectedAssetCount: 2);

        Assert.Equal("同步到 OSS (2)", menuItem.Text);
        Assert.Collection(
            menuItem.DropDownItems.Cast<ToolStripItem>(),
            item => Assert.Equal(projects[0].Id, item.Tag),
            item => Assert.Equal(projects[1].Id, item.Tag),
            item => Assert.Equal(projects[2].Id, item.Tag),
            item => Assert.IsType<ToolStripSeparator>(item),
            item => Assert.Equal("更多...", item.Text));
    }

    [Fact]
    public void SyncToProjectMenu_RequiresJoiningAProjectWhenNoneIsCommon()
    {
        var projects = new[] { CreateProject("项目 1"), CreateProject("项目 2") };
        var selectedAssets = new[]
        {
            CreateAsset("first.mp4", ["项目 1"]),
            CreateAsset("second.mp4", ["项目 2"])
        };
        var commonProjects = MainForm.FindCommonProjects(projects, selectedAssets);
        using var menuItem = new ToolStripMenuItem();

        MainForm.PopulateSyncToProjectMenu(
            menuItem,
            commonProjects,
            selectedAssets.Length);

        Assert.Empty(commonProjects);
        var action = Assert.Single(
            menuItem.DropDownItems.Cast<ToolStripItem>());
        Assert.Equal("加入项目并备份...", action.Text);
    }

    [Fact]
    public void AddAndSyncSelection_OffersExistingOrNewProject()
    {
        using var form = new AssetCollectionSelectionForm(
            [CreateProject("现有项目")],
            selectedAssetCount: 2,
            AssetCollectionSelectionPurpose.AddAndSync);
        form.CreateControl();
        var comboBox = Assert.Single(Descendants(form).OfType<ComboBox>());

        Assert.Equal("加入项目并备份", form.Text);
        Assert.Equal(2, comboBox.Items.Count);
        Assert.Equal("新建项目...", comboBox.GetItemText(comboBox.Items[1]));
        comboBox.SelectedIndex = 1;
        Assert.True(form.CreateNewProject);
        Assert.Null(form.SelectedCollectionId);
    }

    [Fact]
    public void OpenSelection_UsesProjectNavigationWording()
    {
        using var form = new AssetCollectionSelectionForm(
            [CreateProject("项目 A"), CreateProject("项目 B")],
            selectedAssetCount: 1,
            AssetCollectionSelectionPurpose.Open);
        form.CreateControl();

        Assert.Equal("打开所在项目", form.Text);
        Assert.Contains(
            Descendants(form).OfType<Label>(),
            label => label.Text == "选择要打开的所在项目");
        Assert.Contains(
            Descendants(form).OfType<Button>(),
            button => button.Text == "打开");
    }

    [Fact]
    public void ProjectNavigation_FindsMembershipAndSelectsTheAsset()
    {
        var matchingProject = CreateProject("项目 A");
        var projects = new[] { matchingProject, CreateProject("项目 B") };
        var asset = CreateAsset("video.mp4", ["项目 a"]);
        var otherAsset = CreateAsset("other.mp4", ["项目 A"]);

        var matching = MainForm.FindProjectsForAsset(projects, asset);
        using var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add("File", "文件");
        var otherRow = grid.Rows.Add(otherAsset.OriginalFilename);
        grid.Rows[otherRow].Tag = new AssetCollectionMember(
            matchingProject.Id,
            otherAsset,
            DateTimeOffset.UtcNow);
        var assetRow = grid.Rows.Add(asset.OriginalFilename);
        grid.Rows[assetRow].Tag = new AssetCollectionMember(
            matchingProject.Id,
            asset,
            DateTimeOffset.UtcNow);

        var selected = MainForm.SelectProjectMember(grid, asset.AssetId);

        Assert.Equal(matchingProject.Id, Assert.Single(matching).Id);
        Assert.True(selected);
        Assert.Same(grid.Rows[assetRow], grid.CurrentRow);
        Assert.True(grid.Rows[assetRow].Selected);
        Assert.False(grid.Rows[otherRow].Selected);
    }

    [Fact]
    public void ProjectDeletionConfirmation_ListsTheScopeAndPreservesAssets()
    {
        var project = new AssetCollectionSummary(
            Guid.NewGuid(),
            "夏季视频",
            AssetCollectionType.Video,
            AssetCount: 12,
            TotalSizeBytes: 1024,
            BackedUpAssetCount: 5,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        var message = MainForm.CreateProjectDeletionConfirmation(project);

        Assert.Contains("夏季视频", message);
        Assert.Contains("12 个资产", message);
        Assert.Contains("不会删除、移动或修改资产文件", message);
        Assert.Contains("不会删除已有 OSS 备份", message);
        Assert.Contains("无法撤销", message);
    }

    private static AssetCollectionSummary CreateProject(string name)
    {
        return new AssetCollectionSummary(
            Guid.NewGuid(),
            name,
            AssetCollectionType.Mixed,
            AssetCount: 0,
            TotalSizeBytes: 0,
            BackedUpAssetCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    private static ConfiguredObjectStorageProfile CreateBackupProfile(
        string name,
        ObjectStorageProvider provider)
    {
        var now = DateTimeOffset.UtcNow;
        return new ConfiguredObjectStorageProfile(
            new ObjectStorageProfile(
                Guid.NewGuid(),
                name,
                provider,
                "https://storage.example.com",
                "atlas-assets",
                "region-1",
                UseHttps: true,
                "access-key-id",
                now,
                now),
            HasStoredSecret: true);
    }

    private static AssetListItem CreateAsset(
        string filename,
        IReadOnlyList<string> projectNames)
    {
        var now = DateTimeOffset.UtcNow;
        return new AssetListItem(
            Guid.NewGuid(),
            filename,
            Path.GetExtension(filename),
            "video/mp4",
            42,
            null,
            now,
            now,
            Path.Combine(Path.GetTempPath(), filename),
            AssetLocationOwnership.External,
            AssetLocationStatus.Available,
            AssetStatus.Indexed,
            HasHealthyObjectStorageBackup: false)
        {
            ProjectNames = projectNames
        };
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
