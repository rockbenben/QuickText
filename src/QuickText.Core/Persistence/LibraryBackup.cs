using System.IO.Compression;

namespace QuickText.Core.Persistence;

public static class LibraryBackup
{
    public static void Export(string source, string destination)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var target = Path.GetFullPath(destination);
        if (target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a backup location outside the library.");
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var validation = Path.Combine(Path.GetTempPath(), "QuickText-backup-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.CreateFromDirectory(root, temp);
            ZipFile.ExtractToDirectory(temp, validation);
            new Store(validation).ValidateBackup();
            if (File.Exists(target)) File.Replace(temp, target, null);
            else File.Move(temp, target);
        }
        finally
        {
            try { File.Delete(temp); } catch { }
            try { if (Directory.Exists(validation)) Directory.Delete(validation, recursive: true); } catch { }
        }
    }
}
