using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class LegalDocumentsFormTests
{
    [Fact]
    public void Constructor_ShowsBothDocumentsInASelectableReadonlyPanel()
    {
        using var form = new LegalDocumentsForm(
            "Apache license text",
            "Third-party notices text",
            LegalDocumentPage.ThirdPartyNotices);
        var tabs = Assert.Single(Descendants(form).OfType<TabControl>());

        Assert.Equal(["开源协议", "第三方许可"],
            tabs.TabPages.Cast<TabPage>().Select(page => page.Text));
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Equal("许可文档", tabs.AccessibleName);

        var licenseViewer = Assert.Single(
            tabs.TabPages[0].Controls.OfType<RichTextBox>());
        var thirdPartyViewer = Assert.Single(
            tabs.TabPages[1].Controls.OfType<RichTextBox>());
        Assert.Equal("Apache license text", licenseViewer.Text);
        Assert.Equal("Third-party notices text", thirdPartyViewer.Text);
        Assert.All([licenseViewer, thirdPartyViewer], viewer =>
        {
            Assert.True(viewer.ReadOnly);
            Assert.False(viewer.WordWrap);
            Assert.True(viewer.DetectUrls);
            Assert.Equal(RichTextBoxScrollBars.Both, viewer.ScrollBars);
        });
    }

    [Fact]
    public void LoadFromDirectory_ReadsTheBundledLicenseFiles()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "cdsi-agent-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        try
        {
            File.WriteAllText(Path.Combine(testDirectory, "LICENSE"), "license");
            File.WriteAllText(
                Path.Combine(testDirectory, "THIRD-PARTY-NOTICES.md"),
                "notices");

            using var form = LegalDocumentsForm.LoadFromDirectory(
                testDirectory,
                LegalDocumentPage.OpenSourceLicense);
            var tabs = Assert.Single(Descendants(form).OfType<TabControl>());

            Assert.Equal(0, tabs.SelectedIndex);
            Assert.Equal(
                "license",
                Assert.Single(tabs.TabPages[0].Controls.OfType<RichTextBox>()).Text);
            Assert.Equal(
                "notices",
                Assert.Single(tabs.TabPages[1].Controls.OfType<RichTextBox>()).Text);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
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
