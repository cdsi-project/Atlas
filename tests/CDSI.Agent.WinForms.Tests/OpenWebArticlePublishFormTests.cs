using CDSI.Agent.Core.OpenWeb;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class OpenWebArticlePublishFormTests
{
    [Fact]
    public void Form_DefaultsToDraftAndKeepsTheArticleTitleEditable()
    {
        using var form = new OpenWebArticlePublishForm(
            "文章标题",
            Path.Combine(Path.GetTempPath(), "article.md"));
        form.CreateControl();
        var titleTextBox = Descendants(form)
            .OfType<TextBox>()
            .Single(control => control.AccessibleName == "OpenWeb 文章标题");
        var statusComboBox = Descendants(form)
            .OfType<ComboBox>()
            .Single(control => control.AccessibleName == "OpenWeb 发布状态");

        Assert.Equal("文章标题", titleTextBox.Text);
        Assert.Equal(2, statusComboBox.Items.Count);
        Assert.Equal(OpenWebArticleStatus.Draft, form.ArticleStatus);

        statusComboBox.SelectedIndex = 1;
        Assert.Equal(OpenWebArticleStatus.Published, form.ArticleStatus);
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
