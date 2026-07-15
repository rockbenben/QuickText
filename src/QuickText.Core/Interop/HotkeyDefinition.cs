namespace QuickText.Core.Interop;

public sealed class HotkeyDefinition
{
    public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;

    public uint Modifiers { get; init; }
    public uint Vk { get; init; }

    public static HotkeyDefinition Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("empty hotkey");
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        uint mods = 0;
        uint vk = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0) throw new FormatException("empty token");
            switch (p.ToLowerInvariant())
            {
                case "alt": mods |= MOD_ALT; break;
                case "ctrl": case "control": mods |= MOD_CONTROL; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win": mods |= MOD_WIN; break;
                default:
                    vk = KeyToVk(p);
                    break;
            }
        }
        if (vk == 0) throw new FormatException("no key");
        return new HotkeyDefinition { Modifiers = mods, Vk = vk };
    }

    private static uint KeyToVk(string key) => key.ToLowerInvariant() switch
    {
        "space" => 0x20,
        "`" => 0xC0,          // VK_OEM_3
        "enter" or "return" => 0x0D,
        "tab" => 0x09,
        _ when TryFunctionKey(key, out var fvk) => fvk,          // F1..F24 → VK_F1..VK_F24
        _ when key.Length == 1 && char.IsLetterOrDigit(key[0]) => char.ToUpperInvariant(key[0]),
        _ => throw new FormatException($"unknown key: {key}"),
    };

    // "F1".."F24" → 0x70..0x87. Function keys are the one safe SINGLE-key hotkey (they don't type).
    private static bool TryFunctionKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length >= 2 && (key[0] is 'f' or 'F')
            && int.TryParse(key.AsSpan(1), out int n) && n is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + n - 1);
            return true;
        }
        return false;
    }

    public override string ToString()
    {
        var sb = new List<string>();
        if ((Modifiers & MOD_CONTROL) != 0) sb.Add("Ctrl");
        if ((Modifiers & MOD_SHIFT) != 0) sb.Add("Shift");
        if ((Modifiers & MOD_ALT) != 0) sb.Add("Alt");
        if ((Modifiers & MOD_WIN) != 0) sb.Add("Win");
        sb.Add(Vk switch
        {
            0x20 => "Space", 0xC0 => "`", 0x0D => "Enter", 0x09 => "Tab",
            >= 0x70 and <= 0x87 => "F" + (Vk - 0x70 + 1),
            _ => ((char)Vk).ToString(),
        });
        return string.Join("+", sb);
    }
}
