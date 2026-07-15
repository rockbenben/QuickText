using QuickText.Core.Interop;

namespace QuickText.Core.Tests;

public class ModifierTapDetectorTests
{
    private const uint RCtrl = 0xA3;
    private const uint KeyA = 0x41;

    // A controllable clock so double-tap timing is deterministic.
    private sealed class Clock
    {
        public DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public void Advance(int ms) => Now = Now.AddMilliseconds(ms);
    }

    [Fact]
    public void Single_tap_fires_on_clean_press_release()
    {
        var d = new ModifierTapDetector(RCtrl, requireDouble: false);
        Assert.False(d.Feed(RCtrl, isDown: true));
        Assert.True(d.Feed(RCtrl, isDown: false));
    }

    [Fact]
    public void Single_tap_does_not_fire_when_used_in_a_combo()
    {
        var d = new ModifierTapDetector(RCtrl, requireDouble: false);
        d.Feed(RCtrl, true);
        d.Feed(KeyA, true);    // Right Ctrl + A → not a lone tap
        d.Feed(KeyA, false);
        Assert.False(d.Feed(RCtrl, false));
    }

    [Fact]
    public void Single_tap_ignores_auto_repeat()
    {
        var d = new ModifierTapDetector(RCtrl, requireDouble: false);
        d.Feed(RCtrl, true);
        d.Feed(RCtrl, true);   // auto-repeat while held
        d.Feed(RCtrl, true);
        Assert.True(d.Feed(RCtrl, false));
    }

    [Fact]
    public void Double_tap_needs_two_taps_within_the_window()
    {
        var clock = new Clock();
        var d = new ModifierTapDetector(RCtrl, requireDouble: true, () => clock.Now);

        d.Feed(RCtrl, true);
        Assert.False(d.Feed(RCtrl, false));   // first tap — no fire yet
        clock.Advance(150);
        d.Feed(RCtrl, true);
        Assert.True(d.Feed(RCtrl, false));    // second tap within window → fire
    }

    [Fact]
    public void Double_tap_does_not_fire_when_too_slow()
    {
        var clock = new Clock();
        var d = new ModifierTapDetector(RCtrl, requireDouble: true, () => clock.Now);

        d.Feed(RCtrl, true);
        Assert.False(d.Feed(RCtrl, false));
        clock.Advance(ModifierTapDetector.DoubleWindowMs + 100);
        d.Feed(RCtrl, true);
        Assert.False(d.Feed(RCtrl, false));   // too slow → this becomes a new "first" tap
    }

    [Fact]
    public void Double_tap_ignores_a_dirty_first_press()
    {
        var clock = new Clock();
        var d = new ModifierTapDetector(RCtrl, requireDouble: true, () => clock.Now);

        d.Feed(RCtrl, true);
        d.Feed(KeyA, true);                   // dirty (combo) — must not count as the first tap
        d.Feed(KeyA, false);
        Assert.False(d.Feed(RCtrl, false));
        clock.Advance(120);
        d.Feed(RCtrl, true);
        Assert.False(d.Feed(RCtrl, false));   // only one clean tap so far → no fire
    }
}
