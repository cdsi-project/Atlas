using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class AboutFormTests
{
    [Fact]
    public void Constructor_ShowsTheLabeledGitHubRepositoryLink()
    {
        using var form = new AboutForm("0.160");

        var repositoryLink = Assert.Single(
            Descendants(form).OfType<LinkLabel>());

        Assert.Equal(
            "GitHub: github.com/cdsi-project/Atlas",
            repositoryLink.Text);
        Assert.Equal("CDSI Atlas GitHub 仓库", repositoryLink.AccessibleName);
    }

    [Fact]
    public void CreateRepositoryStartInfo_OpensTheOfficialGitHubRepository()
    {
        var startInfo = AboutForm.CreateRepositoryStartInfo();

        Assert.Equal(
            "https://github.com/cdsi-project/Atlas",
            startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
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
