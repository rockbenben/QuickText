using QuickText.Core.Interop;

namespace QuickText.Core.Tests;

public class HotkeyDefinitionTests
{
    [Fact]
    public void Parses_alt_space()
    {
        var h = HotkeyDefinition.Parse("Alt+Space");
        Assert.Equal(1u, h.Modifiers);        // MOD_ALT
        Assert.Equal(0x20u, h.Vk);            // VK_SPACE
    }

    [Fact]
    public void Parses_ctrl_shift_letter()
    {
        var h = HotkeyDefinition.Parse("Ctrl+Shift+K");
        Assert.Equal(2u | 4u, h.Modifiers);   // MOD_CONTROL|MOD_SHIFT
        Assert.Equal((uint)'K', h.Vk);
    }

    [Fact]
    public void Parses_backtick()
    {
        var h = HotkeyDefinition.Parse("Ctrl+`");
        Assert.Equal(2u, h.Modifiers);
        Assert.Equal(0xC0u, h.Vk);            // VK_OEM_3
    }

    [Fact]
    public void Parses_ctrl_shift_digit_round_trips()
    {
        var h = HotkeyDefinition.Parse("Ctrl+Shift+8");
        Assert.Equal(2u | 4u, h.Modifiers);   // MOD_CONTROL|MOD_SHIFT
        Assert.Equal((uint)'8', h.Vk);        // VK 0x38
        Assert.Equal("Ctrl+Shift+8", h.ToString());
    }

    [Fact]
    public void Parses_bare_function_key_round_trips()
    {
        var h = HotkeyDefinition.Parse("F5");
        Assert.Equal(0u, h.Modifiers);         // no modifier — a function key stands alone
        Assert.Equal(0x74u, h.Vk);             // VK_F5
        Assert.Equal("F5", h.ToString());
    }

    [Fact]
    public void Parses_function_keys_f1_and_f24()
    {
        Assert.Equal(0x70u, HotkeyDefinition.Parse("F1").Vk);    // VK_F1
        Assert.Equal(0x87u, HotkeyDefinition.Parse("F24").Vk);   // VK_F24
        Assert.Equal("Ctrl+F12", HotkeyDefinition.Parse("Ctrl+F12").ToString());
    }

    [Fact]
    public void Invalid_throws()
    {
        Assert.Throws<FormatException>(() => HotkeyDefinition.Parse(""));
        Assert.Throws<FormatException>(() => HotkeyDefinition.Parse("Alt+"));
    }
}
