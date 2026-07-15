namespace QuickText.Core.Interop;

/// <summary>Maps the <c>SummonTapKey</c> setting name to its side-specific virtual-key.</summary>
public static class ModifierTapKeys
{
    // Side-specific VKs (the low-level hook reports these, not the generic VK_CONTROL etc.).
    // Only Ctrl/Shift — they have no side effect when tapped alone. Alt activates the menu bar
    // and Win opens the Start menu, so both are excluded as tap keys.
    public static uint? VkOf(string? name) => name switch
    {
        "RCtrl" => 0xA3, "LCtrl" => 0xA2,
        "RShift" => 0xA1, "LShift" => 0xA0,
        _ => null,
    };

    /// <summary>
    /// The single rule for "tap-to-summon is actually active": tap mode selected AND a recognized
    /// modifier key. Shared by the runtime arming (App.UseTapSummon / SetupTapHook) and the Settings
    /// save-time coercion so the persisted/displayed mode can never disagree with the trigger that
    /// fires — tap without a valid key falls back to the combo hotkey everywhere.
    /// </summary>
    public static bool IsValidTap(string? mode, string? key) => mode == "tap" && VkOf(key) != null;
}

/// <summary>
/// Detects a "tap" of a single modifier key — pressed and released with no other key in
/// between — optionally requiring two taps in quick succession (double-tap). Used to summon
/// the panel with e.g. a lone Right Ctrl, which <c>RegisterHotKey</c> can't express (that API
/// only fires on a NON-modifier key). Pure state machine, no Win32, so it can be unit-tested;
/// a keyboard hook feeds it raw key events and acts when <see cref="Feed"/> returns true.
/// </summary>
public sealed class ModifierTapDetector
{
    private readonly uint _targetVk;
    private readonly bool _double;
    private readonly Func<DateTime> _now;

    private bool _held;    // target modifier currently down
    private bool _clean;   // ...and no other key has been pressed since it went down
    private DateTime _lastCleanTap = DateTime.MinValue;

    /// <summary>Max gap between the two taps of a double-tap.</summary>
    public const int DoubleWindowMs = 400;

    public ModifierTapDetector(uint targetVk, bool requireDouble, Func<DateTime>? now = null)
    {
        _targetVk = targetVk;
        _double = requireDouble;
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>Feed one key event. Returns true exactly when the configured tap should fire.</summary>
    public bool Feed(uint vk, bool isDown)
    {
        if (isDown)
        {
            if (vk == _targetVk)
            {
                if (!_held) { _held = true; _clean = true; }   // fresh press; ignore auto-repeat
            }
            else if (_held)
            {
                _clean = false;   // another key while holding the modifier → it's a combo, not a tap
            }
            return false;
        }

        // key up
        if (vk != _targetVk) return false;
        bool wasCleanTap = _held && _clean;
        _held = false;
        _clean = false;
        if (!wasCleanTap) return false;

        if (!_double) return true;   // single-tap: fire immediately

        var now = _now();
        if (_lastCleanTap != DateTime.MinValue && (now - _lastCleanTap).TotalMilliseconds <= DoubleWindowMs)
        {
            _lastCleanTap = DateTime.MinValue;   // consumed — the next double starts fresh
            return true;
        }
        _lastCleanTap = now;   // first tap of a potential double
        return false;
    }
}
