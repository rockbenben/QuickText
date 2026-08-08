using QuickText.Core;

namespace QuickText.Core.Tests;

public class BidiTextTests
{
    [Theory]
    [InlineData("README", false)]
    [InlineData("备注", false)]                       // CJK is a strong left-to-right script
    [InlineData("Привет", false)]
    [InlineData("こんにちは", false)]
    [InlineData("مرحبا", true)]                       // Arabic
    [InlineData("שלום", true)]                        // Hebrew
    [InlineData("ࠀࠁ", true)]                          // Samaritan — upper end of the first block
    // The case this exists for: a name whose first characters are DIGITS. Digits are weak and
    // carry no direction, so the answer must come from the first letter after them — which is why
    // "365 README 备注" was being displayed as "README 备注 365" in an Arabic UI.
    [InlineData("365 README 备注", false)]
    [InlineData("365 مرحبا", true)]
    [InlineData("  \"'-  ", false)]                   // no strong character at all
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Direction_comes_from_the_first_strong_character(string? input, bool rtl)
        => Assert.Equal(rtl, BidiText.IsRightToLeft(input));

    // Mixed content resolves on the FIRST strong character, not on which script has more of it.
    [Fact]
    public void A_later_opposite_script_does_not_flip_the_answer()
    {
        Assert.False(BidiText.IsRightToLeft("README مرحبا مرحبا مرحبا"));
        Assert.True(BidiText.IsRightToLeft("مرحبا README README README"));
    }
}
