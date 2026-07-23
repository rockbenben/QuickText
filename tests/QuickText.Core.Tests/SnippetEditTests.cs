using QuickText.Core.Models;

namespace QuickText.Core.Tests;

public class SnippetEditTests
{
    private static Snippet Sample() => new()
    {
        Name = "签名", Abbr = "qm", Body = "此致\r\n敬礼",
        UseVariables = true, OutputMode = "paste", CodeFormat = "json",
    };

    [Fact]
    public void From_captures_all_six_editable_fields()
    {
        var e = SnippetEdit.From(Sample());
        Assert.Equal("签名", e.Name);
        Assert.Equal("qm", e.Abbr);
        Assert.Equal("此致\r\n敬礼", e.Body);
        Assert.True(e.UseVariables);
        Assert.Equal("paste", e.OutputMode);
        Assert.Equal("json", e.CodeFormat);
    }

    [Fact]
    public void Same_fields_compare_equal()
    {
        Assert.Equal(SnippetEdit.From(Sample()), SnippetEdit.From(Sample()));
    }

    // Each field on its own must be able to make the editor dirty — if any one of these is
    // dropped from the record, edits to that field would silently never prompt and never save.
    [Fact]
    public void Every_field_can_make_it_unequal()
    {
        var baseline = SnippetEdit.From(Sample());
        var s = Sample(); s.Name = "别的"; Assert.NotEqual(baseline, SnippetEdit.From(s));
        s = Sample(); s.Abbr = "x"; Assert.NotEqual(baseline, SnippetEdit.From(s));
        s = Sample(); s.Body = "别的"; Assert.NotEqual(baseline, SnippetEdit.From(s));
        s = Sample(); s.UseVariables = false; Assert.NotEqual(baseline, SnippetEdit.From(s));
        s = Sample(); s.OutputMode = "copy"; Assert.NotEqual(baseline, SnippetEdit.From(s));
        s = Sample(); s.CodeFormat = "sql"; Assert.NotEqual(baseline, SnippetEdit.From(s));
    }

    // THE trap: a snippet that never picked a format has CodeFormat == null, while the UI hands
    // back "". If those don't collapse to the same value, every such snippet reads as dirty the
    // instant it is opened and the user gets a prompt on every single selection change.
    [Fact]
    public void Null_and_empty_code_format_are_the_same_thing()
    {
        var fromNull = SnippetEdit.Of("n", "", "b", false, "", null);
        var fromEmpty = SnippetEdit.Of("n", "", "b", false, "", "");
        Assert.Equal(fromNull, fromEmpty);

        var s = Sample(); s.CodeFormat = null;
        Assert.Equal(SnippetEdit.From(s), SnippetEdit.Of("签名", "qm", "此致\r\n敬礼", true, "paste", ""));
    }

    // Same trap on the other nullable field.
    [Fact]
    public void Null_and_empty_output_mode_are_the_same_thing()
    {
        var s = Sample(); s.OutputMode = null!;
        Assert.Equal(SnippetEdit.From(s), SnippetEdit.Of("签名", "qm", "此致\r\n敬礼", true, "", "json"));
    }

    [Fact]
    public void ApplyTo_writes_the_six_fields_back()
    {
        var target = new Snippet { Name = "旧", Abbr = "old", Body = "旧正文" };
        SnippetEdit.From(Sample()).ApplyTo(target);
        Assert.Equal("签名", target.Name);
        Assert.Equal("qm", target.Abbr);
        Assert.Equal("此致\r\n敬礼", target.Body);
        Assert.True(target.UseVariables);
        Assert.Equal("paste", target.OutputMode);
        Assert.Equal("json", target.CodeFormat);
    }

    // Identity, the image and the timestamp are NOT editor fields: images are an immediate
    // structural operation and the caller owns the timestamp so it only moves on a real change.
    [Fact]
    public void ApplyTo_leaves_id_image_and_timestamp_alone()
    {
        var stamp = DateTimeOffset.UtcNow.AddDays(-3);
        var target = new Snippet { ImagePath = "images/x.png", UpdatedAt = stamp };
        var id = target.Id;
        SnippetEdit.From(Sample()).ApplyTo(target);
        Assert.Equal(id, target.Id);
        Assert.Equal("images/x.png", target.ImagePath);
        Assert.Equal(stamp, target.UpdatedAt);
    }

    // An empty code format must round-trip back to null, not "", or a snippet the user set to
    // plain text would start writing a `codeFormat` field into the library on disk.
    [Fact]
    public void ApplyTo_writes_empty_code_format_back_as_null()
    {
        var target = new Snippet();
        SnippetEdit.Of("n", "", "b", false, "", "").ApplyTo(target);
        Assert.Null(target.CodeFormat);
    }
}
