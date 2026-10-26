using System;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace QuickText.App.Ui;

/// <summary>A focusable Border that a screen reader can see. A plain Border has no automation
/// peer, so the Settings hotkey boxes were invisible to UIA despite being tab stops. The peer
/// presents a button whose name carries the current shortcut and whose Invoke starts capture.</summary>
public sealed class AccessibleHotkeyBox : Border
{
    public event EventHandler? InvokeRequested;

    internal void RequestInvoke() => InvokeRequested?.Invoke(this, EventArgs.Empty);

    internal string ValueText => Child is TextBlock text ? text.Text : "";

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(AccessibleHotkeyBox owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override string GetClassNameCore() => nameof(AccessibleHotkeyBox);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        protected override bool IsKeyboardFocusableCore() => true;

        protected override string GetNameCore()
        {
            var label = base.GetNameCore();
            var value = ((AccessibleHotkeyBox)Owner).ValueText;
            return string.IsNullOrWhiteSpace(value) ? label : $"{label}: {value}";
        }

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        public void Invoke() => ((AccessibleHotkeyBox)Owner).RequestInvoke();
    }
}
