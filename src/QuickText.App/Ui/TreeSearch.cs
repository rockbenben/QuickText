using System.Windows;
using System.Windows.Media;

namespace QuickText.App.Ui;

/// <summary>Tree walks shared by the mouse handlers that ask "which row did this event come from?".</summary>
internal static class TreeSearch
{
    /// <summary>
    /// Nearest ancestor of type <typeparamref name="T"/>, starting at <paramref name="d"/>.
    /// <para>Must handle CONTENT elements, not just visuals. A routed event's OriginalSource inside
    /// a TextBlock is the <c>Run</c> that was actually hit, and a Run is a ContentElement —
    /// <see cref="VisualTreeHelper.GetParent"/> throws "…is not a Visual or Visual3D" on it. That
    /// crash reached users the moment result rows started rendering their text as Runs to accent
    /// the matched characters. Content elements hop to their logical parent (a Run's is the hosting
    /// TextBlock), which puts the walk back on the visual tree and it continues normally.</para>
    /// </summary>
    public static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = d switch
            {
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(d),
                FrameworkContentElement fce => fce.Parent,
                _ => LogicalTreeHelper.GetParent(d),
            };
        }
        return null;
    }
}
