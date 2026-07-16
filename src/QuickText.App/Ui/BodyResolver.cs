using System.Windows;
using QuickText.Core.Snippets;

namespace QuickText.App.Ui;

/// <summary>
/// The single place that turns a snippet body into output text: gates on the per-snippet
/// UseVariables opt-in, prompts for {变量} values, reads the clipboard only when the body
/// uses {剪贴板}, and resolves {光标}/date tokens. The abbreviation hook, panel send, and
/// panel copy all route through here so the three paths can never drift apart.
/// </summary>
public static class BodyResolver
{
    public sealed record Resolved(string Text, int CaretFromEnd, bool HasCursor, bool Prompted);

    /// <summary>Null when the user cancelled the variables prompt.</summary>
    public static Resolved? Resolve(string body, bool useVariables)
    {
        if (!useVariables) return new Resolved(body, 0, false, false);   // opt-out: verbatim

        // Inline {片段:名称} references first, so a nested body's variables prompt too.
        // A referenced snippet with variables OFF is inlined brace-protected (verbatim).
        body = Placeholders.ExpandSnippets(body, AppState.Current.SnippetForNesting);

        var vars = Placeholders.VariableSpecs(body);
        IReadOnlyDictionary<string, string>? values = null;
        bool prompted = false;
        if (vars.Count > 0)
        {
            var filled = VariablesDialog.Fill(vars);
            if (filled == null) return null;   // cancelled
            values = filled;
            prompted = true;   // a modal dialog was shown → focus was stolen
        }

        string clip = "";
        if (Placeholders.UsesClipboard(body))
            try { if (Clipboard.ContainsText()) clip = Clipboard.GetText(); } catch { }

        // Interface-language culture so {日期:dddd} names the weekday in the UI's language.
        var (text, caret, hasCursor) = Placeholders.FillWithCaret(body, values, clip,
            culture: QuickText.Core.Localization.LocalizationService.Instance.Culture);
        text = Placeholders.UnprotectBraces(text);   // restore verbatim nested bodies' literal {…}
        return new Resolved(text, caret, hasCursor, prompted);
    }
}
