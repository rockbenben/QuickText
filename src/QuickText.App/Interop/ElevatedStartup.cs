using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using System.Globalization;
using Microsoft.Win32;
using TaskService = Microsoft.Win32.TaskScheduler.TaskService;
using TaskFolder = Microsoft.Win32.TaskScheduler.TaskFolder;
using TaskCreation = Microsoft.Win32.TaskScheduler.TaskCreation;
using TaskLogonType = Microsoft.Win32.TaskScheduler.TaskLogonType;

namespace QuickText.App.Interop;

public enum StartupOperation { Install, Repair, Uninstall, Plain }
public enum StartupOutcome { Applied, Unchanged, Cancelled, Failed, Uncertain }
public enum StartupFailure { None, AccountMismatch, AccessDenied, OwnershipConflict, RollbackIncomplete }
public sealed record StartupResult(StartupOutcome Outcome, string? Detail = null, StartupFailure Failure = StartupFailure.None);
public sealed class StartupOwnershipException(string message) : InvalidOperationException(message);
public sealed class StartupRollbackException(string message, Exception inner) : IOException(message, inner);
public sealed record StartupOptions(
    string AppId,
    string ExecutableName,
    string TrayArguments = "--tray",
    string? LegacyRunValueName = null,
    string? PlainArguments = null,
    int LogonDelaySeconds = 3,
    bool UseStartupShortcut = false,
    IReadOnlyList<string>? LegacyTaskNames = null);
public sealed record StartupShortcut(string Executable, string Arguments, string WorkingDirectory);
public sealed record StartupTarget(StartupOptions Options, string Executable, string UserSid)
{
    public string TaskName => Options.AppId + ".Startup." + UserSid;
    public string Source => "urn:desktop-startup:" + Options.AppId + ":" + UserSid;
}

// Tests supply a memory implementation; the Windows backend is never needed for isolated tests.
public interface IStartupStore
{
    string? ReadTask(string name);
    void WriteTask(string name, string xml, string userSid);
    void DeleteTask(string name);
    string? ReadRun(string valueName);
    void WriteRun(string valueName, string? command);
    StartupShortcut? ReadShortcut(string name);
    void WriteShortcut(string name, StartupShortcut? shortcut);
}

public static class ElevatedStartup
{
    public const string HelperSwitch = "--startup-task";
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static string NormalizePath(string path)
    {
        string value = path.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        if (value.Contains('"') || !Path.IsPathFullyQualified(value))
            throw new ArgumentException("Expected an absolute executable path, without command arguments.");
        return Path.GetFullPath(value);
    }

    public static StartupTarget Target(StartupOptions options, string executable, string userSid)
    {
        if (!Regex.IsMatch(options.AppId, @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,79}\z") ||
            Path.GetFileName(options.ExecutableName) != options.ExecutableName ||
            !options.ExecutableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            options.ExecutableName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Configure a stable application ID and the published application exe name.");
        ValidateDelay(options.LogonDelaySeconds);
        string path = NormalizePath(executable);
        if (!Path.GetFileName(path).Equals(options.ExecutableName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use Environment.ProcessPath of the published apphost, not a development host.");
        _ = new SecurityIdentifier(userSid);
        return new(options, path, userSid);
    }

    public static void ValidateDelay(int seconds)
    {
        if ((uint)seconds > 300) throw new ArgumentOutOfRangeException(nameof(seconds), "Logon delay must be between 0 and 300 seconds.");
    }

    public static string PlainCommand(StartupTarget target)
    {
        string command = ($"\"{target.Executable}\" " + (target.Options.PlainArguments ?? target.Options.TrayArguments)).TrimEnd();
        if (command.Length > 260) throw new ArgumentException("The Run command exceeds Windows' 260-character limit; use a shorter path or a logon-task backend.");
        return command;
    }

    public static StartupShortcut PlainShortcut(StartupTarget target) => new(target.Executable,
        target.Options.PlainArguments ?? target.Options.TrayArguments, Path.GetDirectoryName(target.Executable)!);

    public static string TaskXml(StartupTarget target)
    {
        ValidateDelay(target.Options.LogonDelaySeconds);
        XElement E(string name, object value) => new(Ns + name, value);
        var trigger = new XElement(Ns + "LogonTrigger", E("Enabled", "true"), E("UserId", target.UserSid));
        if (target.Options.LogonDelaySeconds != 0)
            trigger.Add(E("Delay", XmlConvert.ToString(TimeSpan.FromSeconds(target.Options.LogonDelaySeconds))));
        return new XDocument(new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Source", target.Source)),
            E("Triggers", trigger),
            E("Principals", new XElement(Ns + "Principal", new XAttribute("id", "User"),
                E("UserId", target.UserSid), E("LogonType", "InteractiveToken"), E("RunLevel", "HighestAvailable"))),
            E("Settings", new[] {
                E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"),
                E("StopIfGoingOnBatteries", "false"), E("StartWhenAvailable", "true"),
                E("RunOnlyIfNetworkAvailable", "false"), E("RunOnlyIfIdle", "false"),
                E("Enabled", "true"), E("ExecutionTimeLimit", "PT0S"), E("Priority", "4") }),
            new XElement(Ns + "Actions", new XAttribute("Context", "User"),
                E("Exec", new[] { E("Command", target.Executable), E("Arguments", target.Options.TrayArguments),
                    E("WorkingDirectory", Path.GetDirectoryName(target.Executable)!) })))).ToString();
    }

    private static XElement Root(string xml) => XDocument.Parse(xml).Root
        ?? throw new InvalidDataException("Task XML has no root.");
    private static string Value(XElement root, params string[] path)
    {
        XElement? node = root;
        foreach (string name in path) node = node?.Element(Ns + name);
        return node?.Value ?? "";
    }

    public static bool Owned(string xml, StartupTarget target)
        => Value(Root(xml), "RegistrationInfo", "Source") == target.Source;

    internal static bool BelongsToLegacyQuickText(string xml, string sid)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            string? Val(string name) => doc.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
            var principal = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Principal")?
                .Elements().FirstOrDefault(e => e.Name.LocalName == "UserId")?.Value;
            return Val("Description") == "QuickText logon autostart (elevated)" &&
                string.Equals(principal, sid, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(Val("Command")), "QuickText.exe", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool Matches(string? xml, StartupTarget target)
    {
        if (xml is null) return false;
        try
        {
            var root = Root(xml);
            return Owned(xml, target) &&
                root.Element(Ns + "Actions")?.Elements().Count() == 1 &&
                root.Element(Ns + "Principals")?.Elements().Count() == 1 &&
                root.Element(Ns + "Principals")?.Element(Ns + "Principal")?.Attribute("id")?.Value == "User" &&
                root.Element(Ns + "Actions")?.Attribute("Context")?.Value == "User" &&
                root.Element(Ns + "Triggers")?.Elements().Count() == 1 &&
                SameUser(Value(root, "Principals", "Principal", "UserId"), target.UserSid) &&
                Value(root, "Principals", "Principal", "LogonType") == "InteractiveToken" &&
                Value(root, "Principals", "Principal", "RunLevel") == "HighestAvailable" &&
                SameUser(Value(root, "Triggers", "LogonTrigger", "UserId"), target.UserSid) &&
                BooleanValue(root, true, "Triggers", "LogonTrigger", "Enabled") &&
                !root.Element(Ns + "Triggers")!.Descendants().Any(e =>
                    e.Name == Ns + "StartBoundary" || e.Name == Ns + "EndBoundary" ||
                    e.Name == Ns + "Repetition") &&
                DelayMatches(root, target.Options.LogonDelaySeconds) &&
                NormalizePath(Value(root, "Actions", "Exec", "Command")).Equals(target.Executable, StringComparison.OrdinalIgnoreCase) &&
                Value(root, "Actions", "Exec", "Arguments") == target.Options.TrayArguments &&
                NormalizePath(Value(root, "Actions", "Exec", "WorkingDirectory")).Equals(
                    Path.GetDirectoryName(target.Executable), StringComparison.OrdinalIgnoreCase) &&
                BooleanValue(root, true, "Settings", "Enabled") &&
                Value(root, "Settings", "Priority") == "4" &&
                Value(root, "Settings", "MultipleInstancesPolicy") == "IgnoreNew" &&
                Value(root, "Settings", "DisallowStartIfOnBatteries") == "false" &&
                Value(root, "Settings", "StopIfGoingOnBatteries") == "false" &&
                Value(root, "Settings", "StartWhenAvailable") == "true" &&
                !BooleanValue(root, false, "Settings", "RunOnlyIfNetworkAvailable") &&
                !BooleanValue(root, false, "Settings", "RunOnlyIfIdle") &&
                Value(root, "Settings", "ExecutionTimeLimit") == "PT0S";
        }
        catch { return false; }
    }

    private static bool BooleanValue(XElement root, bool defaultValue, params string[] path)
    {
        XElement? node = root;
        foreach (string name in path) node = node?.Element(Ns + name);
        // Registered task XML omits schema defaults. An explicitly empty or
        // malformed boolean still throws and is rejected by Matches.
        return node is null ? defaultValue : XmlConvert.ToBoolean(node.Value.Trim());
    }

    private static bool SameUser(string value, string expectedSid)
    {
        if (value == expectedSid) return true;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Task Scheduler may serialize the same identity as DOMAIN\User after
        // registration. Resolve it to a SID rather than accepting names by text.
        // Lookup failures propagate to Matches, which rejects the task.
        var sid = value.StartsWith("S-", StringComparison.OrdinalIgnoreCase)
            ? new SecurityIdentifier(value)
            : (SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier));
        return sid.Value == expectedSid;
    }

    private static bool DelayMatches(XElement root, int seconds)
    {
        ValidateDelay(seconds);
        string value = Value(root, "Triggers", "LogonTrigger", "Delay");
        return (value.Length == 0 ? TimeSpan.Zero : XmlConvert.ToTimeSpan(value)) == TimeSpan.FromSeconds(seconds);
    }

    // Register/repair/remove and Run-value cleanup form one transaction. A failed read is never "missing".
    public static StartupResult Apply(StartupOperation operation, StartupTarget target, IStartupStore store)
    {
        // Helper dispatch precedes the app mutex. Separate copies and uninstallers must not race
        // snapshots and rollback against another helper's successful commit.
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.TaskName)));
        using var mutex = new Mutex(false, "DesktopStartup." + key,
            new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = false });
        bool acquired;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw Failure("another startup operation is in progress", target,
            new IOException("Wait for that operation and read state before retrying."));
        try { return ApplyCore(operation, target, store); }
        finally { mutex.ReleaseMutex(); }
    }

    private static StartupResult ApplyCore(StartupOperation operation, StartupTarget target, IStartupStore store)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        string name = target.TaskName;
        string? beforeTask, beforeRun, beforeLegacy;
        StartupShortcut? beforeShortcut;
        var beforeLegacyTasks = new Dictionary<string, string>();
        try
        {
            beforeTask = store.ReadTask(name);
            beforeRun = store.ReadRun(target.Options.AppId);
            beforeLegacy = target.Options.LegacyRunValueName is { } legacy ? store.ReadRun(legacy) : null;
            beforeShortcut = store.ReadShortcut(target.Options.AppId);
            if (beforeTask is not null && !Owned(beforeTask, target))
                throw new StartupOwnershipException("Task name belongs to another application; no changes made.");

            if (target.Options.LegacyTaskNames is { } legacyNames)
            {
                foreach (var legacyName in legacyNames)
                {
                    if (legacyName == name) continue;
                    string? legXml = store.ReadTask(legacyName);
                    if (legXml is not null)
                    {
                        if (Owned(legXml, target) || BelongsToLegacyQuickText(legXml, target.UserSid))
                            beforeLegacyTasks[legacyName] = legXml;
                    }
                }
            }
        }
        catch (Exception ex) { throw Failure("preflight", target, ex); }

        bool remove = operation is StartupOperation.Uninstall or StartupOperation.Plain;
        string? desiredRun = operation == StartupOperation.Plain && !target.Options.UseStartupShortcut ? PlainCommand(target) : null;
        StartupShortcut? desiredShortcut = operation == StartupOperation.Plain && target.Options.UseStartupShortcut ? PlainShortcut(target) : null;
        bool alreadyCorrect = (remove ? beforeTask is null && beforeLegacyTasks.Count == 0 : Matches(beforeTask, target) && beforeLegacyTasks.Count == 0) &&
            beforeRun == desiredRun &&
            beforeLegacy is null &&
            beforeShortcut == desiredShortcut;
        if (alreadyCorrect) return new(StartupOutcome.Unchanged);
        string stage = remove ? "remove task" : "register task";
        try
        {
            if (remove)
            {
                store.DeleteTask(name);
                foreach (var (legName, _) in beforeLegacyTasks)
                    store.DeleteTask(legName);
            }
            else
            {
                store.WriteTask(name, TaskXml(target), target.UserSid);
                foreach (var (legName, _) in beforeLegacyTasks)
                    store.DeleteTask(legName);
            }

            stage = "verify task";
            string? after = store.ReadTask(name);
            if (remove ? after is not null : !Matches(after, target))
                throw new IOException("Task readback does not match the requested state. " +
                    $"Expected XML: {(remove ? "<absent>" : TaskXml(target))}; " +
                    $"Actual XML: {after ?? "<absent>"}");

            stage = "update plain startup";
            store.WriteRun(target.Options.AppId, desiredRun);
            if (target.Options.LegacyRunValueName is { } legacy) store.WriteRun(legacy, null);
            store.WriteShortcut(target.Options.AppId, desiredShortcut);
            if (store.ReadRun(target.Options.AppId) != desiredRun ||
                (target.Options.LegacyRunValueName is { } legacyCheck && store.ReadRun(legacyCheck) is not null) ||
                store.ReadShortcut(target.Options.AppId) != desiredShortcut)
                throw new IOException("Plain startup entry could not be verified after update.");
            return new(StartupOutcome.Applied);
        }
        catch (Exception ex)
        {
            // Attempt all restores even if one fails, and report any incomplete rollback honestly.
            var errors = new List<Exception> { ex };
            try
            {
                if (beforeTask is null) store.DeleteTask(name);
                else store.WriteTask(name, beforeTask, target.UserSid);
                foreach (var (legName, legXml) in beforeLegacyTasks)
                    store.WriteTask(legName, legXml, target.UserSid);

                string? restored = store.ReadTask(name);
                if (beforeTask is null ? restored is not null : restored is null ||
                    !XNode.DeepEquals(Root(beforeTask), Root(restored)))
                    throw new IOException("Previous task definition could not be verified after rollback.");
            }
            catch (Exception rollback) { errors.Add(rollback); }
            try
            {
                store.WriteRun(target.Options.AppId, beforeRun);
                if (store.ReadRun(target.Options.AppId) != beforeRun)
                    throw new IOException("Previous Run entry could not be verified after rollback.");
            }
            catch (Exception rollback) { errors.Add(rollback); }
            if (target.Options.LegacyRunValueName is { } legacy)
            {
                try
                {
                    store.WriteRun(legacy, beforeLegacy);
                    if (store.ReadRun(legacy) != beforeLegacy)
                        throw new IOException("Legacy Run entry could not be verified after rollback.");
                }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            try
            {
                store.WriteShortcut(target.Options.AppId, beforeShortcut);
                if (store.ReadShortcut(target.Options.AppId) != beforeShortcut)
                    throw new IOException("Previous shortcut entry could not be verified after rollback.");
            }
            catch (Exception rollback) { errors.Add(rollback); }
            var failure = Failure(stage + (errors.Count > 1 ? "; rollback incomplete" : "; previous startup restored"), target,
                new AggregateException(errors));
            if (errors.Count > 1) throw new StartupRollbackException(failure.Message, failure);
            throw failure;
        }
    }

    private static Exception Failure(string stage, StartupTarget target, Exception error)
        => new IOException($"Startup {stage}; task: {target.TaskName}; executable: {target.Executable}; " +
            $"error: 0x{error.HResult:X8}; {error.Message}", error);

    public static StartupFailure Classify(Exception error)
    {
        if (error is StartupRollbackException) return StartupFailure.RollbackIncomplete;
        if (error is StartupOwnershipException) return StartupFailure.OwnershipConflict;
        if (error is UnauthorizedAccessException || error.HResult == unchecked((int)0x80070005))
            return StartupFailure.AccessDenied;
        if (error is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
                if (Classify(inner) is var failure && failure != StartupFailure.None) return failure;
        }
        return error.InnerException is { } nested ? Classify(nested) : StartupFailure.None;
    }

    public static StartupOperation ParseHelper(string[] args, string currentSid)
    {
        if (args.Length is not (3 or 4) || args[0] != HelperSwitch)
            throw new ArgumentException("Invalid startup-helper request. No changes made.");
        if (args.Length == 4) _ = HelperDelay(args);
        if (args[2] != currentSid)
            throw new UnauthorizedAccessException("Different UAC account; this template requires the requesting administrator's own account. No changes made.");
        return args[1] switch {
            "install" => StartupOperation.Install, "repair" => StartupOperation.Repair,
            "uninstall" => StartupOperation.Uninstall, "plain" => StartupOperation.Plain,
            _ => throw new ArgumentException("Unknown startup-helper operation.") };
    }

    public static int HelperDelay(string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds))
            throw new ArgumentException("Invalid startup delay. No changes made.");
        ValidateDelay(seconds);
        return seconds;
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevationTypeClass = 18;
    private const int TokenElevationTypeLimitedValue = 3;

    /// <summary>
    /// Checks whether the current user is an administrator account. Under UAC, unelevated processes
    /// run with a split/limited token where BUILTIN\Administrators is marked Deny-Only and filtered by .NET.
    /// This method queries TokenElevationType to reliably detect administrator status before triggering UAC.
    /// </summary>
    public static bool IsAdministratorAccount()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator)) return true;

            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_QUERY, out var hToken))
            {
                try
                {
                    int size = sizeof(int);
                    IntPtr pElevationType = Marshal.AllocHGlobal(size);
                    try
                    {
                        if (GetTokenInformation(hToken, TokenElevationTypeClass, pElevationType, (uint)size, out _))
                        {
                            int elevationType = Marshal.ReadInt32(pElevationType);
                            if (elevationType == TokenElevationTypeLimitedValue) return true;
                        }
                    }
                    finally { Marshal.FreeHGlobal(pElevationType); }
                }
                finally { CloseHandle(hToken); }
            }
        }
        catch { }
        return false;
    }

    // Call before the application mutex, settings writes, tray icon, hooks or ordinary CLI dispatch.
    // Supply the app's logger; map the exit status to localized UI in the requesting process.
    public static bool TryHandleHelper(string[] args, StartupOptions options, Action<Exception> log, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] != HelperSwitch) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var operation = ParseHelper(args, identity.User!.Value);
            if (!IsAdministrator()) throw new UnauthorizedAccessException("Startup helper requires UAC elevation.");
            if (args.Length == 4) options = options with { LogonDelaySeconds = HelperDelay(args) };
            var target = Target(options, Environment.ProcessPath ?? "", identity.User.Value);
            if (!File.Exists(target.Executable)) throw new FileNotFoundException("Application exe is missing.");
            Apply(operation, target, new WindowsStartupStore());
        }
        catch (Exception ex)
        {
            exitCode = ex is UnauthorizedAccessException ? 5 : Classify(ex) switch
            {
                StartupFailure.RollbackIncomplete => 6,
                StartupFailure.AccessDenied => 7,
                StartupFailure.OwnershipConflict => 8,
                _ => 4,
            };
            try { log(ex); } catch { /* Keep the failure exit status even if logging fails. */ }
        }
        return true;
    }

    // STA worker keeps ShellExecute/UAC and task COM off the WPF dispatcher. Timeout is uncertainty,
    // not cancellation: do not kill or replay a helper that may already have committed a task.
    private static int _helperPending;
    public static bool IsHelperPending => Volatile.Read(ref _helperPending) != 0;

    public static Task<StartupResult> ChangeAsync(StartupOperation operation, StartupOptions options)
    {
        if (Interlocked.CompareExchange(ref _helperPending, 1, 0) != 0)
            return Task.FromResult(new StartupResult(StartupOutcome.Uncertain, "A startup helper is still running."));
        var completion = new TaskCompletionSource<StartupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            bool watching = false;
            StartupResult result;
            try
            {
                if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
                using var identity = WindowsIdentity.GetCurrent();
                var target = Target(options, Environment.ProcessPath ?? "", identity.User!.Value);
                var store = new WindowsStartupStore();

                // Check if in-process execution is possible without UAC
                bool taskPresent = false;
                try
                {
                    taskPresent = store.ReadTask(target.TaskName) != null;
                    if (!taskPresent && target.Options.LegacyTaskNames is { } legacyNames)
                    {
                        foreach (var leg in legacyNames)
                        {
                            if (store.ReadTask(leg) != null) { taskPresent = true; break; }
                        }
                    }
                }
                catch { /* if read fails, helper will handle under UAC */ }

                bool canRunInProcess = IsAdministrator() ||
                    ((operation is StartupOperation.Uninstall or StartupOperation.Plain) && !taskPresent);

                if (canRunInProcess)
                {
                    try
                    {
                        result = Apply(operation, target, store);
                        Interlocked.Exchange(ref _helperPending, 0);
                        completion.SetResult(result);
                        return;
                    }
                    catch (Exception ex) when (Classify(ex) == StartupFailure.AccessDenied)
                    {
                        // Needs elevation; fall through to UAC helper below
                    }
                }

                result = Request(operation, target, info =>
                {
                    var process = Process.Start(info) ?? throw new IOException("Could not start the UAC helper.");
                    if (process.WaitForExit(60000))
                    {
                        using (process) return process.ExitCode;
                    }
                    watching = true;
                    _ = Task.Run(() =>
                    {
                        try { process.WaitForExit(); }
                        finally { process.Dispose(); Interlocked.Exchange(ref _helperPending, 0); }
                    });
                    return null;
                }, store);
            }
            catch (Exception ex) { result = new(StartupOutcome.Failed, ex.ToString(), Classify(ex)); }
            if (!watching) Interlocked.Exchange(ref _helperPending, 0);
            completion.SetResult(result);
        }) { IsBackground = true };
        try
        {
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _helperPending, 0);
            completion.SetResult(new(StartupOutcome.Failed, ex.ToString()));
        }
        return completion.Task;
    }

    // runHelper returns null on timeout. Injection here verifies UAC outcomes without launching a process.
    public static StartupResult Request(StartupOperation operation, StartupTarget target,
        Func<ProcessStartInfo, int?> runHelper, IStartupStore store)
    {
        bool committed = false;
        try
        {
            if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
            var info = new ProcessStartInfo(target.Executable) {
                UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(target.Executable)!,
                WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(HelperSwitch);
            info.ArgumentList.Add(operation.ToString().ToLowerInvariant());
            ValidateDelay(target.Options.LogonDelaySeconds);
            info.ArgumentList.Add(target.UserSid);
            info.ArgumentList.Add(target.Options.LogonDelaySeconds.ToString(CultureInfo.InvariantCulture));
            int? code = runHelper(info);
            if (code is null) return new(StartupOutcome.Uncertain, "Startup helper is still running; read state before retrying.");
            if (code == 5) return new(StartupOutcome.Failed, "UAC helper must be elevated in the requesting administrator's own account; no changes made.", StartupFailure.AccountMismatch);
            if (code is 6 or 7 or 8) return new(StartupOutcome.Failed, $"Startup helper exited {code}; see startup-helper.log.",
                code == 6 ? StartupFailure.RollbackIncomplete : code == 7 ? StartupFailure.AccessDenied : StartupFailure.OwnershipConflict);
            if (code != 0) return new(StartupOutcome.Failed, $"Startup helper exited {code}; see the application log.");
            committed = true;
            bool valid = operation is StartupOperation.Uninstall or StartupOperation.Plain ? store.ReadTask(target.TaskName) is null
                : Matches(store.ReadTask(target.TaskName), target);
            string? desiredRun = operation == StartupOperation.Plain && !target.Options.UseStartupShortcut ? PlainCommand(target) : null;
            StartupShortcut? desiredShortcut = operation == StartupOperation.Plain && target.Options.UseStartupShortcut ? PlainShortcut(target) : null;
            return valid && store.ReadRun(target.Options.AppId) == desiredRun &&
                (target.Options.LegacyRunValueName is not { } legacy || store.ReadRun(legacy) is null) &&
                store.ReadShortcut(target.Options.AppId) == desiredShortcut
                ? new(StartupOutcome.Applied) : new(StartupOutcome.Uncertain, "Post-helper readback failed; read state before retrying.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { return new(StartupOutcome.Cancelled); }
        catch (Exception ex) { return new(committed ? StartupOutcome.Uncertain : StartupOutcome.Failed, ex.ToString(), Classify(ex)); }
    }
}

// Windows-only adapter. The engine above is independently testable without touching OS state.
public sealed class WindowsStartupStore : IStartupStore
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static T WithFolder<T>(Func<TaskFolder, T> action)
    {
        using var service = new TaskService();
        using var folder = service.RootFolder;
        return action(folder);
    }

    // Only call around a task lookup in an already-open folder. The collection indexer
    // reports missing tasks as ERROR_PATH_NOT_FOUND too; connection/folder failures stay errors.
    internal static string? ReadTaskXml(Func<string> read)
    {
        try { return read(); }
        catch (Exception ex) when (ex.HResult is unchecked((int)0x80070002) or unchecked((int)0x80070003))
        { return null; }
    }

    public string? ReadTask(string name) => WithFolder<string?>(folder =>
    {
        // TaskService.GetTask and TaskCollection.Exists swallow COM errors. The
        // named collection indexer propagates access denial and other query failures.
        using var tasks = folder.Tasks;
        return ReadTaskXml(() =>
        {
            using var task = tasks[name];
            return task.Xml;
        });
    });
    public void WriteTask(string name, string xml, string userSid) => WithFolder(folder =>
    {
        // Explicit DACL prevents an ordinary-user principal ACE from permitting task modification.
        // Do not add a principal write ACE or fire registration triggers during restore.
        using var task = folder.RegisterTask(name, xml,
            TaskCreation.CreateOrUpdate | TaskCreation.DontAddPrincipalAce | TaskCreation.IgnoreRegistrationTriggers,
            userSid, null, TaskLogonType.InteractiveToken, $"D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FR;;;{userSid})");
        return true;
    });
    public void DeleteTask(string name) => WithFolder(folder =>
    {
        try { folder.DeleteTask(name); }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070002)) { }
        return true;
    });
    public string? ReadRun(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        object? value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is not null && (value is not string || key!.GetValueKind(valueName) != RegistryValueKind.String))
            throw new IOException("Unexpected Run value type; preserve it and migrate explicitly.");
        return (string?)value;
    }
    public void WriteRun(string valueName, string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (command is null) key.DeleteValue(valueName, false);
        else key.SetValue(valueName, command, RegistryValueKind.String);
    }
    private readonly StartupShortcutStore shortcuts = new(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
    public StartupShortcut? ReadShortcut(string name) => shortcuts.Read(name);
    public void WriteShortcut(string name, StartupShortcut? shortcut) => shortcuts.Write(name, shortcut);
}

// Only shortcut files live here: tests use a temporary directory without a task/registry adapter.
internal sealed class StartupShortcutStore(string directory)
{
    private string ShortcutPath(string name)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new IOException("The Startup directory is unavailable.");
        return Path.Combine(directory, name + ".lnk");
    }

    private static string ReadText(Action<StringBuilder, int> read)
    {
        // Shell link APIs can silently truncate to capacity - 1. Grow until the value fits;
        // reject excessive data rather than taking a truncated rollback snapshot.
        const int maximumCapacity = 1024 * 1024;
        for (int capacity = 260; ; capacity = Math.Min(capacity * 2, maximumCapacity))
        {
            var buffer = new StringBuilder(capacity);
            read(buffer, capacity);
            if (buffer.Length < capacity - 1) return buffer.ToString();
            if (capacity == maximumCapacity) throw new InvalidDataException("Shortcut text exceeds the supported buffer size.");
        }
    }

    public StartupShortcut? Read(string name)
    {
        string path = ShortcutPath(name);
        // File.Exists also returns false for access/read errors. Only proven absence is null.
        try { _ = File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        object? link = null;
        try
        {
            link = (IShellLinkW)new ShellLink();
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Load(path, 0);
            var shellLink = (IShellLinkW)link;
            return new(ReadText((buffer, capacity) => shellLink.GetPath(buffer, capacity, IntPtr.Zero, 0)),
                ReadText(shellLink.GetArguments), ReadText(shellLink.GetWorkingDirectory));
        }
        finally { Release(link); }
    }

    public void Write(string name, StartupShortcut? shortcut)
    {
        string path = ShortcutPath(name);
        if (shortcut is null)
        {
            try { File.Delete(path); }
            catch (DirectoryNotFoundException) { /* The startup directory itself is absent. */ }
            return;
        }
        object? link = null;
        try
        {
            link = (IShellLinkW)new ShellLink();
            ((IShellLinkW)link).SetPath(shortcut.Executable);
            ((IShellLinkW)link).SetArguments(shortcut.Arguments);
            ((IShellLinkW)link).SetWorkingDirectory(shortcut.WorkingDirectory);
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(path, false);
        }
        finally { Release(link); }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink { }

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
    void Resolve(IntPtr hwnd, int fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}
