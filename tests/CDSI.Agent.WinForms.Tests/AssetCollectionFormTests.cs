using CDSI.Agent.Core.Collections;
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
    public void CollectionLayout_KeepsListsInSeparateResizablePanes()
    {
        using var collectionGrid = new DataGridView();
        using var memberGrid = new DataGridView();
        using var createButton = new Button();
        using var removeButton = new Button();
        using var syncButton = new Button();
        using var layout = MainForm.CreateAssetCollectionLayout(
            collectionGrid,
            memberGrid,
            createButton,
            removeButton,
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
