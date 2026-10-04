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

    // ---- BuildCombo: the capture-side seam (batch6 F1). Keyboard.Modifiers can't report the
    // Windows key, so Win arrives as an explicit flag; these pin that the flag survives into the
    // string Parse consumes — the round-trip is the real proof, not a hand-written expectation. ----

    [Fact]
    public void BuildCombo_win_shift_letter_keeps_the_win_half()
    {
        var combo = HotkeyDefinition.BuildCombo("S", ctrl: false, shift: true, alt: false, win: true, bareKeyOk: false);
        // Canonical modifier order matches ToString: Ctrl, Shift, Alt, Win.
        Assert.Equal("Shift+Win+S", combo);
        Assert.Equal(4u | 8u, HotkeyDefinition.Parse(combo!).Modifiers);   // MOD_SHIFT|MOD_WIN
        Assert.Equal(combo, HotkeyDefinition.Parse(combo!).ToString());    // round-trips
    }

    [Fact]
    public void BuildCombo_win_alone_with_letter_is_usable()
    {
        // A bare letter with only Win held must NOT be rejected as "needs a modifier" — Win counts.
        var combo = HotkeyDefinition.BuildCombo("D", false, false, false, win: true, bareKeyOk: false);
        Assert.Equal("Win+D", combo);
    }

    [Fact]
    public void BuildCombo_typing_key_without_modifier_is_rejected()
    {
        Assert.Null(HotkeyDefinition.BuildCombo("K", false, false, false, win: false, bareKeyOk: false));
    }

    [Fact]
    public void BuildCombo_bare_function_key_is_allowed()
    {
        Assert.Equal("F5", HotkeyDefinition.BuildCombo("F5", false, false, false, win: false, bareKeyOk: true));
    }

    [Fact]
    public void BuildCombo_null_token_is_rejected()
    {
        Assert.Null(HotkeyDefinition.BuildCombo(null, ctrl: true, shift: false, alt: false, win: false, bareKeyOk: false));
    }

    [Fact]
    public void BuildCombo_round_trips_through_parse()
    {
        var combo = HotkeyDefinition.BuildCombo("Space", ctrl: true, shift: false, alt: true, win: false, bareKeyOk: false);
        Assert.Equal("Ctrl+Alt+Space", combo);
        Assert.Equal(combo, HotkeyDefinition.Parse(combo!).ToString());
    }
}
