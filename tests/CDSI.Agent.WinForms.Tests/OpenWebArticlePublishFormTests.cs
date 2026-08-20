using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.OpenWeb;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class OpenWebArticlePublishFormTests
{
    [Fact]
    public void Form_DefaultsToDraftAndKeepsTheArticleTitleEditable()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new ConfiguredOpenWebSource(
            new OpenWebSource(
                Guid.NewGuid(),
                "备用站",
                "backup.example.com",
                "editor",
                false,
                now,
                now),
            true);
        var defaultSource = new ConfiguredOpenWebSource(
            new OpenWebSource(
                Guid.NewGuid(),
                "主站",
                "main.example.com",
                "editor",
                true,
                now,
                now),
            true);
        using var form = new OpenWebArticlePublishForm(
            "文章标题",
            Path.Combine(Path.GetTempPath(), "article.md"),
            [first, defaultSource]);
        form.CreateControl();
        var titleTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control => control.AccessibleName == "OpenWeb 文章标题");
        var statusComboBox = Descendants(form)
            .OfType<ComboBox>()
            .Single(control => control.AccessibleName == "OpenWeb 发布状态");
        var sourceComboBox = Descendants(form)
            .OfType<ComboBox>()
            .Single(control => control.AccessibleName == "OpenWeb 目标源站");

        Assert.Equal("文章标题", titleTextBox.Text);
        Assert.Equal(2, statusComboBox.Items.Count);
        Assert.Equal(OpenWebArticleStatus.Draft, form.ArticleStatus);
        Assert.Equal(2, sourceComboBox.Items.Count);
        Assert.Equal(defaultSource.Source.Id, form.SourceId);

        statusComboBox.SelectedIndex = 1;
        Assert.Equal(OpenWebArticleStatus.Published, form.ArticleStatus);
        sourceComboBox.SelectedIndex = 0;
        Assert.Equal(first.Source.Id, form.SourceId);
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
