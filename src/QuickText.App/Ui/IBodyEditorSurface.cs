using System;

namespace QuickText.App.Ui;

/// <summary>
/// What <see cref="BodyEditorWindow"/> needs from whichever editor it is hosting. Implemented by
/// BodyEditor (plain text, native TextBox — keeps full Arabic bidi) and CodeEditor (AvalonEdit,
/// syntax highlighting). Exactly one is alive at a time; switching format swaps the instance.
/// </summary>
internal interface IBodyEditorSurface
{
    string Text { get; set; }
    int CaretIndex { get; set; }
    bool UseVariables { get; set; }
    Func<string, bool>? SnippetExists { get; set; }
    void FocusEditor();
}
