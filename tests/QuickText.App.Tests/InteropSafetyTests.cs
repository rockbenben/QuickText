using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using QuickText.App.Interop;

namespace QuickText.App.Tests;

public class InteropSafetyTests
{
    [Fact]
    public void Each_input_step_checks_the_original_target()
    {
        IntPtr original = new(1), current = original;
        var sent = new List<string>();
        Assert.True(ForegroundTarget.TrySend(original, () => current, () => sent.Add("paste")));
        current = new IntPtr(2);
        Assert.False(ForegroundTarget.TrySend(original, () => current, () => sent.Add("enter")));
        Assert.False(ForegroundTarget.TrySend(original, () => current, () => sent.Add("left")));
        Assert.False(ForegroundTarget.TrySend(original, () => current, () => sent.Add("backspace")));
        Assert.False(ForegroundTarget.TrySend(IntPtr.Zero, () => IntPtr.Zero, () => sent.Add("invalid")));
        Assert.Equal(new[] { "paste" }, sent);
    }

    [Fact]
    public void Elevated_task_xml_and_ownership_require_application_and_account()
    {
        var options = new StartupOptions("QuickText", "QuickText.exe", "--autostart", LogonDelaySeconds: 3);
        var target = ElevatedStartup.Target(options, @"C:\Program Files\QuickText\QuickText.exe", "S-1-5-21-1");
        var xml = ElevatedStartup.TaskXml(target);

        Assert.Contains("<Priority>4</Priority>", xml);
        Assert.Contains("<Delay>PT3S</Delay>", xml);
        Assert.Contains(@"<WorkingDirectory>C:\Program Files\QuickText</WorkingDirectory>", xml);
        Assert.Contains(@"<Command>C:\Program Files\QuickText\QuickText.exe</Command>", xml);
        Assert.Contains("<Arguments>--autostart</Arguments>", xml);
        Assert.True(ElevatedStartup.Owned(xml, target));
        Assert.True(ElevatedStartup.Matches(xml, target));

        var otherTarget = ElevatedStartup.Target(options, @"C:\Program Files\QuickText\QuickText.exe", "S-1-5-21-2");
        Assert.False(ElevatedStartup.Owned(xml, otherTarget));
        Assert.False(ElevatedStartup.Matches(xml, otherTarget));
    }

    [Fact]
    public void Legacy_quicktext_task_format_is_recognized()
    {
        const string legacyXml = "<Task><Description>QuickText logon autostart (elevated)</Description><Principal><UserId>S-1-5-21-1</UserId></Principal><Command>C:\\Program Files\\QuickText\\QuickText.exe</Command></Task>";
        Assert.True(ElevatedStartup.BelongsToLegacyQuickText(legacyXml, "S-1-5-21-1"));
        Assert.False(ElevatedStartup.BelongsToLegacyQuickText(legacyXml, "S-1-5-21-2"));
        Assert.False(ElevatedStartup.BelongsToLegacyQuickText(legacyXml.Replace("QuickText.exe", "other.exe"), "S-1-5-21-1"));
        Assert.False(ElevatedStartup.BelongsToLegacyQuickText(legacyXml.Replace("QuickText logon autostart (elevated)", "Unrelated"), "S-1-5-21-1"));
    }

    [Fact]
    public void Helper_command_validation_rejects_unauthorized_invocations()
    {
        Assert.Throws<UnauthorizedAccessException>(() =>
            ElevatedStartup.ParseHelper(new[] { "--startup-task", "install", "S-1-0-0" }, "S-1-0-1"));
        Assert.Throws<ArgumentException>(() =>
            ElevatedStartup.ParseHelper(new[] { "--invalid", "install", "S-1-0-0" }, "S-1-0-0"));
        Assert.Throws<ArgumentException>(() =>
            ElevatedStartup.ParseHelper(new[] { "--startup-task", "unknown-op", "S-1-0-0" }, "S-1-0-0"));

        var op = ElevatedStartup.ParseHelper(new[] { "--startup-task", "install", "S-1-0-0" }, "S-1-0-0");
        Assert.Equal(StartupOperation.Install, op);
    }

    [Fact]
    public void Administrator_account_is_detected_under_uac()
    {
        Assert.True(ElevationPolicy.IsAdministratorAccount());
    }

    [Fact]
    public void Valid_executable_can_be_registered_by_administrator()
    {
        Assert.True(ElevationPolicy.CanRegister(Environment.ProcessPath!));
    }

    [Fact]
    public void Writable_executables_and_untrusted_ACL_owners_cannot_be_elevated()
    {
        Assert.False(ElevationPolicy.CanRegister(Path.Combine(Path.GetTempPath(), "QuickText.exe")));
        var security = new FileSecurity();
        var admin = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        security.SetOwner(admin);
        security.AddAccessRule(new FileSystemAccessRule(admin, FileSystemRights.FullControl, AccessControlType.Allow));
        Assert.True(ElevationPolicy.HasProtectedAcl(security));
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.WriteData, AccessControlType.Allow));
        Assert.False(ElevationPolicy.HasProtectedAcl(security));
        security.SetOwner(users);
        Assert.False(ElevationPolicy.HasProtectedAcl(security, ancestor: true));
    }

    [Fact]
    public void Autostart_portable_plain_creates_shortcut_and_clears_registry_and_task()
    {
        var store = new MockStartupStore { RunCommand = "old run" };
        var opt = Autostart.DefaultOptions with { UseStartupShortcut = true };
        var res = Autostart.Apply(enabled: true, elevated: false, store, opt);
        Assert.Equal(StartupOutcome.Applied, res.Outcome);
        Assert.NotNull(store.Shortcut);
        Assert.Null(store.RunCommand);
        Assert.Null(store.TaskXml);
    }

    [Fact]
    public void Autostart_installed_plain_writes_registry_and_clears_shortcut_and_task()
    {
        var store = new MockStartupStore { Shortcut = new StartupShortcut("C:\\path\\QuickText.exe", "--autostart", "C:\\path") };
        var opt = Autostart.DefaultOptions with { UseStartupShortcut = false };
        var res = Autostart.Apply(enabled: true, elevated: false, store, opt);
        Assert.Equal(StartupOutcome.Applied, res.Outcome);
        Assert.NotNull(store.RunCommand);
        Assert.Null(store.Shortcut);
        Assert.Null(store.TaskXml);
    }

    [Fact]
    public void Autostart_elevated_registers_task_and_clears_both_entries()
    {
        var store = new MockStartupStore
        {
            RunCommand = "old run",
            Shortcut = new StartupShortcut("C:\\path\\QuickText.exe", "--autostart", "C:\\path")
        };
        var res = Autostart.Apply(enabled: true, elevated: true, store);
        Assert.Equal(StartupOutcome.Applied, res.Outcome);
        Assert.NotNull(store.TaskXml);
        Assert.Null(store.RunCommand);
        Assert.Null(store.Shortcut);
    }

    [Fact]
    public void Autostart_rollback_restores_task_and_entries_when_mutation_fails()
    {
        var store = new MockStartupStore
        {
            RunCommand = "previous run",
            FailWriteOnce = true
        };
        var ex = Assert.Throws<IOException>(() => Autostart.Apply(enabled: true, elevated: false, store));
        Assert.Contains("previous startup restored", ex.Message);
        Assert.Equal("previous run", store.RunCommand);
    }

    [Fact]
    public void Autostart_rollback_reports_incomplete_when_restore_also_fails()
    {
        var store = new MockStartupStore
        {
            RunCommand = "previous run",
            FailOnWrite = true
        };
        var ex = Assert.Throws<StartupRollbackException>(() => Autostart.Apply(enabled: true, elevated: false, store));
        Assert.Contains("rollback incomplete", ex.Message);
    }

    [Fact]
    public void Autostart_disabled_clears_all_entries()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var opt = Autostart.DefaultOptions;
        string exePath = Autostart.ResolveExePath(opt.ExecutableName);
        var target = ElevatedStartup.Target(opt, exePath, identity.User!.Value);
        var store = new MockStartupStore
        {
            TaskXml = ElevatedStartup.TaskXml(target),
            RunCommand = "run",
            Shortcut = new StartupShortcut(exePath, "--autostart", Path.GetDirectoryName(exePath)!)
        };
        var res = Autostart.Apply(enabled: false, elevated: false, store);
        Assert.Equal(StartupOutcome.Applied, res.Outcome);
        Assert.Null(store.TaskXml);
        Assert.Null(store.RunCommand);
        Assert.Null(store.Shortcut);
    }

    private sealed class MockStartupStore : IStartupStore
    {
        public string? TaskXml { get; set; }
        public string? RunCommand { get; set; }
        public StartupShortcut? Shortcut { get; set; }
        public bool FailWriteOnce { get; set; }
        public bool FailOnWrite { get; set; }
        public List<string> Trace { get; } = [];

        public string? ReadTask(string name) => TaskXml;
        public void WriteTask(string name, string xml, string userSid)
        {
            Trace.Add($"WriteTask:{name}");
            if (FailWriteOnce) { FailWriteOnce = false; throw new IOException("Mock Task write failed"); }
            if (FailOnWrite) throw new IOException("Mock Task write failed");
            TaskXml = xml;
        }
        public void DeleteTask(string name)
        {
            Trace.Add($"DeleteTask:{name}");
            if (FailWriteOnce) { FailWriteOnce = false; throw new IOException("Mock Task delete failed"); }
            if (FailOnWrite) throw new IOException("Mock Task delete failed");
            TaskXml = null;
        }

        public string? ReadRun(string valueName) => RunCommand;
        public void WriteRun(string valueName, string? command)
        {
            Trace.Add($"WriteRun:{command ?? "null"}");
            if (FailWriteOnce) { FailWriteOnce = false; throw new IOException("Mock Run write failed"); }
            if (FailOnWrite) throw new IOException("Mock Run write failed");
            RunCommand = command;
        }

        public StartupShortcut? ReadShortcut(string name) => Shortcut;
        public void WriteShortcut(string name, StartupShortcut? shortcut)
        {
            Trace.Add($"WriteShortcut:{(shortcut != null ? shortcut.Executable : "null")}");
            if (FailWriteOnce) { FailWriteOnce = false; throw new IOException("Mock Shortcut write failed"); }
            if (FailOnWrite) throw new IOException("Mock Shortcut write failed");
            Shortcut = shortcut;
        }
    }
}
