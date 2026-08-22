using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class AboutFormTests
{
    private const string ClientId = "0192f4f7-7f2b-7f6a-a4d2-438f0df34a19";

    [Fact]
    public void Constructor_ShowsTheLabeledGitHubRepositoryLink()
    {
        using var form = new AboutForm("0.160", ClientId);

        var repositoryLink = Assert.Single(
            Descendants(form).OfType<LinkLabel>());

        Assert.Equal(
            "GitHub: github.com/cdsi-project/Beacon",
            repositoryLink.Text);
        Assert.Equal("CDSI Beacon GitHub 仓库", repositoryLink.AccessibleName);
    }

    [Fact]
    public void Constructor_ShowsTheClientIdAndCopyCommand()
    {
        using var form = new AboutForm("0.201", ClientId);

        var clientIdTextBox = Assert.Single(
            Descendants(form).OfType<TextBox>(),
            control => control.AccessibleName == "客户端 ID");
        var copyButton = Assert.Single(
            Descendants(form).OfType<Button>(),
            control => control.AccessibleName == "复制客户端 ID");

        Assert.Equal(ClientId, clientIdTextBox.Text);
        Assert.True(clientIdTextBox.ReadOnly);
        Assert.Equal("复制", copyButton.Text);
    }

    [Fact]
    public void Constructor_ShowsThePackagedBeaconLogo()
    {
        using var form = new AboutForm("0.201", ClientId);

        var logo = Assert.Single(
            Descendants(form).OfType<PictureBox>(),
            control => control.AccessibleName == "CDSI Beacon 标识");

        Assert.NotNull(logo.Image);
        Assert.Equal(1254, logo.Image.Width);
        Assert.Equal(1254, logo.Image.Height);
        Assert.Equal(PictureBoxSizeMode.Zoom, logo.SizeMode);
    }

    [Fact]
    public void CreateRepositoryStartInfo_OpensTheOfficialGitHubRepository()
    {
        var startInfo = AboutForm.CreateRepositoryStartInfo();

        Assert.Equal(
            "https://github.com/cdsi-project/Beacon",
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
