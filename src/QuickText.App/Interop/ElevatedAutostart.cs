using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.ComponentModel;

namespace QuickText.App.Interop;

/// <summary>
/// "Start with Windows as administrator" toggle. A logon task registered with
/// <c>RunLevel=HighestAvailable</c> is the only autostart mechanism Windows will launch elevated
/// without a UAC prompt at every login: Run keys and Startup-folder shortcuts are always started
/// unelevated, so an app run as admin for pasting into elevated windows otherwise loses that on
/// every reboot.
/// <para>Registering or deleting the task needs elevation. The app itself stays
/// <c>asInvoker</c> (see app.manifest) — marking the whole exe
/// <c>requireAdministrator</c> would prompt at EVERY logon. Instead a non-elevated instance
/// relaunches itself once with <c>Verb=runas</c> and a one-shot subcommand; that child does the
/// task operation and exits without building a window, tray or single-instance mutex.</para>
/// <para>Task operations go through the Task Scheduler COM API, not <c>schtasks.exe</c>:
/// schtasks reports failures as localized text, so "access denied" cannot be classified on the
/// non-Chinese/English systems this app ships in. COM returns the language-independent
/// <c>E_ACCESSDENIED</c> HRESULT instead.</para>
/// </summary>
public static class ElevatedAutostart
{
    /// <summary>One-shot subcommands handled in <c>App.OnStartup</c> before the single-instance
    /// check. The elevated child is launched with exactly one of these and nothing else.</summary>
    public const string RegisterFlag = "--register-elevated-autostart";
    public const string UnregisterFlag = "--unregister-elevated-autostart";

    public const string TaskName = "QuickText";

    // ITaskFolder.RegisterTask / DeleteTask constants (taskschd.h).
    private const int TaskCreateOrUpdate = 6;          // TASK_CREATE_OR_UPDATE
    private const int TaskLogonInteractiveToken = 3;   // TASK_LOGON_INTERACTIVE_TOKEN

    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ErrorCancelled = 1223;           // ERROR_CANCELLED: user declined the UAC prompt

    // Environment.ProcessPath is the real exe path even in a single-file build.
    private static string ExePath => Environment.ProcessPath ?? "";

    /// <summary>How a task operation ended. NeedsAdmin is not a failure: it is the signal to hand
    /// the work to the elevated one-shot child. Error details go to the log — the child has no UI,
    /// and the parent shows one localized sentence rather than .NET text in 17 wrong languages.</summary>
    private enum TaskOutcome { Ok, NeedsAdmin, Error }

    /// <summary>Run the one-shot elevated subcommand. Returns the process exit code to use, or
    /// null when <paramref name="args"/> is a normal launch.</summary>
    public static int? RunSubcommand(string[] args)
    {
        bool register = Array.Exists(args, a => a == RegisterFlag);
        if (!register && !Array.Exists(args, a => a == UnregisterFlag)) return null;
        var res = register ? Register(ExePath) : Unregister();
        return res == TaskOutcome.Ok ? 0 : 2;
    }

    /// <summary>(task exists, task launches THIS exe). The Task Scheduler service being unavailable
    /// is not an error here — it reads as off rather than blocking Settings. A task that exists but
    /// points at an old location is still ours (the name is ours): <c>UpToDate=false</c> is what
    /// makes the next save rewrite it instead of answering "already applied" forever.</summary>
    public static (bool Exists, bool UpToDate) Probe()
    {
        bool exists = false, upToDate = false;
        try
        {
            RunTaskOp(folder =>
            {
                dynamic? task = null;
                try
                {
                    try { task = folder.GetTask(TaskName); }
                    // GetTask throws when the task is absent — that IS "not registered", not a failure.
                    catch { return; }
                    exists = true;
                    upToDate = PointsHere(task);
                }
                finally { if (task != null) { try { Marshal.ReleaseComObject((object)task); } catch { } } }
            });
        }
        catch { return (false, false); }   // service unavailable reads as off rather than blocking Settings
        return (exists, upToDate);
    }

    /// <summary>Does the registered XML launch the exe we are running now? Anything this cannot read
    /// answers "yes": a false "stale" costs a UAC prompt on every save that re-registering may not
    /// even fix. Namespace-agnostic, because the schema namespace and the 1.0 form differ.</summary>
    private static bool PointsHere(dynamic task)
    {
        try
        {
            var cmd = System.Xml.Linq.XDocument.Parse((string)task.Xml).Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Command")?.Value;
            if (cmd == null) return true;
            return string.Equals(cmd.Trim(), ExePath, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    /// <summary>Register the elevated logon task.</summary>
    private static TaskOutcome Register(string exePath) => Guard(() =>
    {
        var user = SecurityElement.Escape(Environment.UserDomainName + "\\" + Environment.UserName) ?? "";
        var cmd = SecurityElement.Escape(exePath) ?? "";
        // RegisterTask takes the XML as a UTF-16 string, so no temp file and no UTF-16+BOM dance
        // (which is what schtasks.exe /XML would need).
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>QuickText logon autostart (elevated)</Description></RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger></Triggers>
  <Principals><Principal id=""Author""><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author""><Exec><Command>{cmd}</Command><Arguments>{Autostart.Flag}</Arguments></Exec></Actions>
</Task>";
        RunTaskOp(folder =>
        {
            dynamic? task = null;
            try { task = folder.RegisterTask(TaskName, xml, TaskCreateOrUpdate, null, null, TaskLogonInteractiveToken, null); }
            finally { if (task != null) { try { Marshal.ReleaseComObject((object)task); } catch { } } }
        });
    });

    /// <summary>Remove the elevated logon task. Idempotent: absent is already the target state.</summary>
    private static TaskOutcome Unregister() => Guard(() =>
    {
        if (!Probe().Exists) return;
        RunTaskOp(folder => folder.DeleteTask(TaskName, 0));
    });

    /// <summary>
    /// Enable/disable elevated autostart from the (usually unelevated) running instance, or repair a
    /// task that points at an old exe location.
    /// <para>Try in-process first: when the task is already absent (the common case for turning it
    /// off, and for switching to normal autostart) or the app already runs elevated, that succeeds
    /// with NO UAC prompt. Only a language-independent E_ACCESSDENIED result triggers the one-shot
    /// <c>runas</c> child.</para>
    /// <para>A declined UAC prompt is <see cref="AutostartResult.Declined"/>: not an error, but it
    /// MUST stop the caller from deleting the ordinary autostart entry — otherwise one cancelled
    /// prompt leaves the user with no autostart at all.</para>
    /// </summary>
    public static AutostartResult Apply(bool enable)
    {
        var res = enable ? Register(ExePath) : Unregister();
        if (res == TaskOutcome.Ok) return AutostartResult.Applied;
        if (res != TaskOutcome.NeedsAdmin) return AutostartResult.Failed;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                Arguments = enable ? RegisterFlag : UnregisterFlag,
                Verb = "runas",
                UseShellExecute = true,
            };
            using var p = Process.Start(psi);
            if (p == null)
            {
                QuickText.Core.Log.Write("elevated autostart: the elevated helper did not start");
                return AutostartResult.Failed;
            }
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                // The helper logged its own reason; all the parent can know is that it failed.
                QuickText.Core.Log.Write($"elevated autostart: helper exited with {p.ExitCode}");
                return AutostartResult.Failed;
            }
            return AutostartResult.Applied;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return AutostartResult.Declined;   // declined UAC: no change, no complaint
        }
        catch (Exception ex)
        {
            QuickText.Core.Log.Error("elevated autostart runas", ex);
            return AutostartResult.Failed;
        }
    }

    private static TaskOutcome Guard(Action body)
    {
        try { body(); return TaskOutcome.Ok; }
        catch (UnauthorizedAccessException) { return TaskOutcome.NeedsAdmin; }
        catch (SecurityException) { return TaskOutcome.NeedsAdmin; }
        catch (Exception ex)
        {
            // The one failure that means "elevate to do this", classified on a number rather than on
            // localized text — schtasks-style messages are unreadable in the 16 other languages.
            if (ex.HResult == EAccessDenied) return TaskOutcome.NeedsAdmin;
            QuickText.Core.Log.Error("elevated autostart", ex);
            return TaskOutcome.Error;
        }
    }

    private static void RunTaskOp(Action<dynamic> op)
    {
        var svcType = Type.GetTypeFromProgID("Schedule.Service") ?? throw new Exception("Schedule.Service unavailable");
        dynamic? svc = null;
        dynamic? folder = null;
        try
        {
            svc = Activator.CreateInstance(svcType);
            svc!.Connect();
            folder = svc.GetFolder("\\");
            op(folder);
        }
        finally
        {
            if (folder != null) { try { Marshal.ReleaseComObject(folder); } catch { } }
            if (svc != null) { try { Marshal.ReleaseComObject(svc); } catch { } }
        }
    }
}
