using System.IO.Compression;

namespace QuickText.Core.Updates;

/// <summary>
/// The only place a downloaded update is unpacked and marked usable. The completion marker is the
/// trust signal: a half-written exe from an interrupted extraction must never be offered as a
/// ready update just because the file name exists.
/// </summary>
public static class UpdateInstaller
{
    public const string CompletionMarkerName = ".complete";
    public const string ExpectedExecutableName = "QuickText.exe";

    /// <summary>The executable of a COMPLETED install in <paramref name="destDir"/>, else null.</summary>
    public static string? FindInstalledExecutable(string destDir)
    {
        if (!Directory.Exists(destDir)) return null;
        var exe = Path.Combine(destDir, ExpectedExecutableName);
        return File.Exists(Path.Combine(destDir, CompletionMarkerName)) && File.Exists(exe)
            ? exe : null;
    }

    /// <summary>
    /// Extract into a private staging folder, verify the expected program exists, write the
    /// completion marker and only then replace the destination. A bad archive or a write failure
    /// leaves the previous install untouched.
    /// </summary>
    public static string InstallFromZip(string zipPath, string destDir)
    {
        var fullDest = Path.GetFullPath(destDir);
        var parent = Path.GetDirectoryName(fullDest)
                     ?? throw new InvalidOperationException("update destination has no parent folder");
        Directory.CreateDirectory(parent);

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(fullDest));
        var staging = Path.Combine(parent, name + ".staging-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(parent, name + ".old-" + Guid.NewGuid().ToString("N"));
        bool hasBackup = false;
        try
        {
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);

            var exe = Path.Combine(staging, ExpectedExecutableName);
            if (!File.Exists(exe))
                throw new InvalidDataException("the downloaded archive does not contain " + ExpectedExecutableName);
            File.WriteAllText(Path.Combine(staging, CompletionMarkerName), "");

            // Move the old folder aside instead of deleting it first. If the final move fails
            // (AV lock, sharing violation), put it back so the previous install survives.
            if (Directory.Exists(fullDest))
            {
                Directory.Move(fullDest, backup);
                hasBackup = true;
            }
            Directory.Move(staging, fullDest);
            try { if (hasBackup) Directory.Delete(backup, recursive: true); } catch { }
            hasBackup = false;
            return Path.Combine(fullDest, ExpectedExecutableName);
        }
        catch
        {
            if (hasBackup && !Directory.Exists(fullDest))
            {
                try { Directory.Move(backup, fullDest); hasBackup = false; }
                catch { /* leave the .old folder: better than deleting the only install */ }
            }
            throw;
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
            if (!hasBackup)
            {
                try { if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true); } catch { }
            }
        }
    }
}
