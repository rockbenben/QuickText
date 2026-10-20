using QuickText.Core;

namespace QuickText.Core.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v1.0.1", "1.0.0")]   // patch bump, tag carries a leading 'v'
    [InlineData("1.1.0", "1.0.9")]    // minor beats a higher patch
    [InlineData("2.0.0", "1.9.9")]    // major beats everything below
    [InlineData("v1.0.1", "1.0")]     // more components: extra 1 beats the implicit trailing 0
    [InlineData("1.0.0", "0.9")]      // fewer components on the older side
    public void Newer_tag_is_an_update(string latest, string current)
        => Assert.True(UpdateCheck.IsNewer(latest, current));

    [Theory]
    [InlineData("v1.0.0", "1.0.0")]   // identical
    [InlineData("1.0.0", "1.0.0.0")]  // trailing zeros don't make it newer
    [InlineData("v1.0.0", "1.0.1")]   // older tag than current
    [InlineData("0.9.0", "1.0.0")]
    public void Same_or_older_tag_is_not_an_update(string latest, string current)
        => Assert.False(UpdateCheck.IsNewer(latest, current));

    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("", "1.0.0")]
    [InlineData("nightly", "1.0.0")]   // non-numeric tag never reports a phantom update
    [InlineData("v1.0.0", "garbage")]
    public void Unparsable_input_is_never_an_update(string? latest, string? current)
        => Assert.False(UpdateCheck.IsNewer(latest, current));

    [Fact]
    public void Prerelease_suffix_is_ignored_for_comparison()
    {
        Assert.True(UpdateCheck.IsNewer("v1.2.0-beta", "1.1.0"));   // 1.2.0 > 1.1.0
        Assert.False(UpdateCheck.IsNewer("v1.0.0-rc1", "1.0.0"));   // 1.0.0 == 1.0.0
    }

    [Theory]
    [InlineData("v1.4.2", "1.4.2")]
    [InlineData("V1.4.2", "1.4.2")]
    [InlineData("1.4.2", "1.4.2")]
    [InlineData("", "")]
    public void Normalize_version_drops_the_leading_v(string tag, string expected)
        => Assert.Equal(expected, UpdateCheck.NormalizeVersion(tag));

    [Fact]
    public void The_self_contained_zip_is_picked_and_not_the_framework_dependent_one()
    {
        // A release carries both; the framework-dependent one needs .NET installed, so handing it
        // to a user as "just run this" would give them an exe that refuses to start.
        string[] assets =
        [
            "QuickText-v1.4.2-win-x64-needs-dotnet10.zip",
            "QuickText-v1.4.2-win-x64.zip",
            "QuickText.exe",
            "SHA256SUMS.txt",
        ];
        Assert.Equal("QuickText-v1.4.2-win-x64.zip", UpdateCheck.SelectSelfContainedAsset(assets));
    }

    [Fact]
    public void No_self_contained_asset_means_no_pick()
    {
        string[] onlyFrameworkDependent =
        [
            "QuickText-v1.4.2-win-x64-needs-dotnet10.zip",
            "QuickText.exe",
            "SHA256SUMS.txt",
        ];
        Assert.Null(UpdateCheck.SelectSelfContainedAsset(onlyFrameworkDependent));
        Assert.Null(UpdateCheck.SelectSelfContainedAsset([]));
    }

    [Fact]
    public void Checksums_are_parsed_by_file_name()
    {
        // The format sha256sum writes: digest, whitespace, name. The '*' marks binary mode.
        string text =
            "a388a698d2fbf5b303bb3bd39a46d95311e3731d34f7790c06460581ae9923b9  QuickText-v1.4.2-win-x64-needs-dotnet10.zip\r\n"
          + "cb0216f8f5ccb03681c4cf3ddaa39bf8ece16319c9f860cf7b0d5c277297e0ce *QuickText-v1.4.2-win-x64.zip\r\n"
          + "8cade4ee4ed3de5b0197d11b8d2f2e57ada0304376f1a381451d0d9e7c3b5154  QuickText.exe\r\n";
        var sums = UpdateCheck.ParseChecksums(text);
        Assert.Equal(3, sums.Count);
        Assert.Equal("cb0216f8f5ccb03681c4cf3ddaa39bf8ece16319c9f860cf7b0d5c277297e0ce",
                     sums["QuickText-v1.4.2-win-x64.zip"]);
        Assert.Equal("8cade4ee4ed3de5b0197d11b8d2f2e57ada0304376f1a381451d0d9e7c3b5154",
                     sums["QuickText.exe"]);
    }

    [Fact]
    public void Lines_that_are_not_a_digest_are_skipped()
    {
        // A release also ships a README and a licence; their lines are not ours to read.
        string text =
            "# SHA256SUMS.txt\r\n"
          + "not-a-digest  QuickText.exe\r\n"
          + "cb0216f8f5ccb03681c4cf3ddaa39bf8ece16319c9f860cf7b0d5c277297e0ce  QuickText.exe\r\n";
        var sums = UpdateCheck.ParseChecksums(text);
        Assert.Single(sums);
        Assert.True(sums.ContainsKey("QuickText.exe"));
    }

    [Fact]
    public void The_digest_of_a_file_is_computed_and_compared()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllBytes(path, "quicktext"u8.ToArray());
        try
        {
            string digest = UpdateCheck.ComputeSha256Hex(path);
            Assert.Equal(64, digest.Length);
            Assert.True(UpdateCheck.MatchesChecksum(digest.ToUpperInvariant(), digest));   // case carries no meaning
            string altered = (digest[0] == 'a' ? "b" : "a") + digest[1..];
            Assert.False(UpdateCheck.MatchesChecksum(altered, digest));
            Assert.False(UpdateCheck.MatchesChecksum(null, digest));
            Assert.False(UpdateCheck.MatchesChecksum(digest, ""));
        }
        finally { File.Delete(path); }
    }
}
