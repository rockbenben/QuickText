using QuickText.Core.Snippets;

namespace QuickText.Core.Tests;

public class OutputPolicyTests
{
    [Theory]
    [InlineData("", true, true, false, false, true, false)]
    [InlineData("", true, true, false, true, true, false)]
    [InlineData("paste", true, true, false, true, false, false)]
    [InlineData("paste-enter", true, false, false, true, false, true)]
    [InlineData("paste-enter", false, false, true, false, false, false)]
    [InlineData("copy", false, true, false, false, true, false)]
    [InlineData("", false, true, false, true, false, false)]
    [InlineData("", false, true, false, false, false, true)]
    public void Output_policy_is_shared_by_text_images_and_entry_points(string mode, bool globalCopy,
        bool globalEnter, bool cursor, bool abbreviation, bool copy, bool enter)
    {
        var output = OutputPolicy.Resolve(mode, globalCopy, globalEnter, cursor, abbreviation);
        Assert.Equal(copy, output.CopyOnly);
        Assert.Equal(enter, output.AutoSend);
    }
}
