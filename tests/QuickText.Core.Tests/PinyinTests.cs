using QuickText.Core.Pinyin;

namespace QuickText.Core.Tests;

public class PinyinTests
{
    private readonly IPinyinProvider _p = new ToolGoodPinyinProvider();

    [Fact]
    public void Full_pinyin_of_nihao()
    {
        Assert.Equal("nihao", _p.GetFullPinyin("你好"));
    }

    [Fact]
    public void Initials_of_nihao()
    {
        Assert.Equal("nh", _p.GetInitials("你好"));
    }

    [Fact]
    public void Ascii_passes_through_lowercased()
    {
        Assert.Equal("hi", _p.GetFullPinyin("Hi"));
        Assert.Equal("h", _p.GetInitials("Hi"));
    }

    [Fact]
    public void Initials_resolve_polyphone_yinyue_contextually()
    {
        // 乐 in 音乐 (yinyue) is "yue", not the non-contextual first candidate "le".
        Assert.Equal("yy", _p.GetInitials("音乐"));
    }

    [Fact]
    public void Initials_resolve_polyphone_chongqing_contextually()
    {
        // 重 in 重庆 (chongqing) is "chong", not the non-contextual first candidate "zhong".
        Assert.Equal("cq", _p.GetInitials("重庆"));
    }

    [Fact]
    public void Initials_of_mixed_ascii_and_chinese()
    {
        // The leading "AB" run (non-Chinese) contributes only its first character, "a".
        Assert.Equal("anh", _p.GetInitials("AB你好"));
    }
}
