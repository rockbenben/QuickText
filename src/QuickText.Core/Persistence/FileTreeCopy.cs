namespace QuickText.Core.Persistence;

/// <summary>
/// Copy a whole directory tree over an existing one, with a rollback point. Used by backup import:
/// a failure halfway through must not leave a mixed live library.
/// </summary>
public static class FileTreeCopy
{
    public static void CopyWithRollback(string from, string to)
    {
        var source = Path.GetFullPath(from);
        var dest = Path.GetFullPath(to);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        Directory.CreateDirectory(dest);

        var rollback = Path.Combine(Path.GetTempPath(),
            "QuickText-rollback-" + Guid.NewGuid().ToString("N"));
        var created = new List<string>();
        var backups = new List<(string Target, string Backup)>();

        try
        {
            // Deterministic order lets a caller/test reason about which file failed, and makes a
            // partial run reproducible.
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.GetFullPath(Path.Combine(dest, relative));
                if (!IsUnder(target, dest))
                    throw new InvalidDataException("backup contains a path outside the destination: " + relative);

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    var backup = Path.Combine(rollback, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: true);
                    backups.Add((target, backup));
                }
                else
                {
                    created.Add(target);
                }
                File.Copy(file, target, overwrite: true);
            }
        }
        catch
        {
            // New files first, then restore every file that had existed before the import.
            for (int i = created.Count - 1; i >= 0; i--)
                try { File.Delete(created[i]); } catch { }
            for (int i = backups.Count - 1; i >= 0; i--)
                try { File.Copy(backups[i].Backup, backups[i].Target, overwrite: true); } catch { }
            throw;
        }
        finally
        {
            try { if (Directory.Exists(rollback)) Directory.Delete(rollback, recursive: true); } catch { }
        }
    }

    private static bool IsUnder(string fullPath, string root)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
