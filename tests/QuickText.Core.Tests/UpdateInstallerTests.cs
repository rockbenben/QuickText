using System.IO.Compression;
using QuickText.Core.Updates;

namespace QuickText.Core.Tests;

public class UpdateInstallerTests : IDisposable
{
    private readonly string _work =
        Path.Combine(Path.GetTempPath(), "khupt_" + Guid.NewGuid().ToString("N"));

    public UpdateInstallerTests() => Directory.CreateDirectory(_work);
    public void Dispose() { try { Directory.Delete(_work, true); } catch { } }

    private string Zip(params (string Name, byte[] Data)[] files)
    {
        var path = Path.Combine(_work, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, data) in files)
        {
            var entry = archive.CreateEntry(name);
            using var stream = entry.Open();
            stream.Write(data);
        }
        return path;
    }

    private string ValidZip() => Zip((UpdateInstaller.ExpectedExecutableName, "program"u8.ToArray()));

    [Fact]
    public void Installed_executable_requires_both_the_expected_exe_and_completion_marker()
    {
        var dest = Path.Combine(_work, "1.0.0");
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, UpdateInstaller.ExpectedExecutableName), "x");
        Assert.Null(UpdateInstaller.FindInstalledExecutable(dest));

        File.WriteAllText(Path.Combine(dest, UpdateInstaller.CompletionMarkerName), "");
        Assert.Equal(Path.Combine(dest, UpdateInstaller.ExpectedExecutableName),
                     UpdateInstaller.FindInstalledExecutable(dest));
    }

    [Fact]
    public void Install_extracts_verifies_and_commits_the_new_executable()
    {
        var dest = Path.Combine(_work, "1.2.3");
        var installed = UpdateInstaller.InstallFromZip(ValidZip(), dest);

        Assert.Equal(Path.Combine(dest, UpdateInstaller.ExpectedExecutableName), installed);
        Assert.True(File.Exists(installed));
        Assert.True(File.Exists(Path.Combine(dest, UpdateInstaller.CompletionMarkerName)));
        Assert.Equal(installed, UpdateInstaller.FindInstalledExecutable(dest));
    }

    [Fact]
    public void Install_replaces_a_partial_destination_without_a_completion_marker()
    {
        var dest = Path.Combine(_work, "1.2.3");
        Directory.CreateDirectory(dest);
        File.WriteAllText(Path.Combine(dest, UpdateInstaller.ExpectedExecutableName), "partial");
        Assert.Null(UpdateInstaller.FindInstalledExecutable(dest));

        UpdateInstaller.InstallFromZip(ValidZip(), dest);

        Assert.Equal("program", File.ReadAllText(Path.Combine(dest, UpdateInstaller.ExpectedExecutableName)));
        Assert.NotNull(UpdateInstaller.FindInstalledExecutable(dest));
    }

    [Fact]
    public void A_bad_archive_never_replaces_the_previous_install()
    {
        var dest = Path.Combine(_work, "1.2.3");
        UpdateInstaller.InstallFromZip(ValidZip(), dest);
        var exe = Path.Combine(dest, UpdateInstaller.ExpectedExecutableName);
        var before = File.ReadAllBytes(exe);

        var bad = Path.Combine(_work, "bad.zip");
        File.WriteAllText(bad, "not a zip");
        Assert.ThrowsAny<Exception>(() => UpdateInstaller.InstallFromZip(bad, dest));

        Assert.Equal(before, File.ReadAllBytes(exe));
        Assert.Equal(exe, UpdateInstaller.FindInstalledExecutable(dest));
    }

    [Fact]
    public void A_zip_without_the_expected_program_is_rejected()
    {
        var dest = Path.Combine(_work, "1.2.3");
        var zip = Zip(("other.exe", "x"u8.ToArray()));

        Assert.ThrowsAny<Exception>(() => UpdateInstaller.InstallFromZip(zip, dest));
        Assert.Null(UpdateInstaller.FindInstalledExecutable(dest));
    }
}
