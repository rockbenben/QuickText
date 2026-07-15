using QuickText.Core;

namespace QuickText.Core.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v1.0.1", "1.0.0")]   // patch bump, tag carries a leading 'v'
    [InlineData("1.1.0", "1.0.9")]    // minor beats a higher patch
    [InlineData("2.0.0", "1.9.9")]    // major beats everything below
    [InlineData("v1.0.1", "1.0")]     // more components: extra 1 beats the implicit trailing 0
    [InlineData("1.0.0", "0.9")]      // fewer components on the older side
    public void Newer_tag_is_an_update(string latest, string current)
        => Assert.True(UpdateCheck.IsNewer(latest, current));

    [Theory]
    [InlineData("v1.0.0", "1.0.0")]   // identical
    [InlineData("1.0.0", "1.0.0.0")]  // trailing zeros don't make it newer
    [InlineData("v1.0.0", "1.0.1")]   // older tag than current
    [InlineData("0.9.0", "1.0.0")]
    public void Same_or_older_tag_is_not_an_update(string latest, string current)
        => Assert.False(UpdateCheck.IsNewer(latest, current));

    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("", "1.0.0")]
    [InlineData("nightly", "1.0.0")]   // non-numeric tag never reports a phantom update
    [InlineData("v1.0.0", "garbage")]
    public void Unparsable_input_is_never_an_update(string? latest, string? current)
        => Assert.False(UpdateCheck.IsNewer(latest, current));

    [Fact]
    public void Prerelease_suffix_is_ignored_for_comparison()
    {
        Assert.True(UpdateCheck.IsNewer("v1.2.0-beta", "1.1.0"));   // 1.2.0 > 1.1.0
        Assert.False(UpdateCheck.IsNewer("v1.0.0-rc1", "1.0.0"));   // 1.0.0 == 1.0.0
    }
}
