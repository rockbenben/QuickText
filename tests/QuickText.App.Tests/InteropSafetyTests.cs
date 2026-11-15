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
    public void Elevated_task_ownership_requires_both_application_and_account()
    {
        const string xml = "<Task><Description>QuickText logon autostart (elevated)</Description><Principal><UserId>S-1-5-21-1</UserId></Principal><Command>C:\\Program Files\\QuickText\\QuickText.exe</Command></Task>";
        Assert.True(ElevatedAutostart.BelongsTo(xml, "S-1-5-21-1", "PC\\User"));
        Assert.False(ElevatedAutostart.BelongsTo(xml, "S-1-5-21-2", "PC\\Other"));
        Assert.False(ElevatedAutostart.BelongsTo(xml.Replace("QuickText.exe", "other.exe"), "S-1-5-21-1", "PC\\User"));
        Assert.False(ElevatedAutostart.BelongsTo(xml.Replace("QuickText logon autostart (elevated)", "Unrelated task"), "S-1-5-21-1", "PC\\User"));
        Assert.Equal(2, ElevatedAutostart.RunSubcommand(new[] { ElevatedAutostart.RegisterFlag, "S-1-0-0" }));
    }

    [Fact]
    public void Task_xml_contains_normal_priority_and_working_directory()
    {
        var xml = ElevatedAutostart.BuildTaskXml(@"C:\Program Files\QuickText\QuickText.exe", "S-1-5-21-1");
        Assert.Contains("<Priority>4</Priority>", xml);
        Assert.Contains(@"<WorkingDirectory>C:\Program Files\QuickText</WorkingDirectory>", xml);
        Assert.Contains(@"<Command>C:\Program Files\QuickText\QuickText.exe</Command>", xml);
        Assert.True(ElevatedAutostart.BelongsTo(xml, "S-1-5-21-1", "PC\\User"));
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
}
