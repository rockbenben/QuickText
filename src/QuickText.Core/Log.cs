namespace QuickText.Core;

/// <summary>
/// Append-one-line-to-a-file error log. Deliberately not a logging framework: the only question
/// ever asked of it is "the app vanished / something silently didn't happen — what was it?", and
/// answering that needs a timestamp and a stack trace the user can paste into an issue, not sinks,
/// levels and structured queries.
/// <para>Lives beside <c>settings.json</c> (<see cref="AppPaths.MachineStateDir"/>), so it inherits
/// the portable/installed split for free: a USB copy writes into <c>Data\</c> and leaves nothing on
/// the host machine.</para>
/// <para>Every failure in here is swallowed, which is the one place that rule is right. This is the
/// handler of last resort — if recording someone else's crash throws, there is nowhere left to
/// report it, and letting that escape would turn a survivable fault into the very crash the log
/// exists to explain.</para>
/// </summary>
public static class Log
{
    /// <summary>Truncate past this size. Rotation would mean a second file to find and manage, which
    /// buys nothing when the only interesting entries are the most recent ones.</summary>
    private const long MaxBytes = 256 * 1024;

    private static readonly object _gate = new();

    public static string FilePath => Path.Combine(AppPaths.MachineStateDir, "error.log");

    public static void Error(string context, Exception? ex) => Write($"{context}: {ex}");

    public static void Write(string message)
    {
        try
        {
            lock (_gate)
            {
                var path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch { /* see the class remarks — nowhere left to report this */ }
    }
}
