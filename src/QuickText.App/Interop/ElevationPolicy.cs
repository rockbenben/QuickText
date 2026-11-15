using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace QuickText.App.Interop;

internal static class ElevationPolicy
{
    private const int TokenElevationType = 18;
    private const int TokenElevationTypeLimited = 3;
    private const uint TokenQuery = 0x0008;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private static readonly HashSet<string> TrustedOwners = new(StringComparer.Ordinal)
    {
        "S-1-5-18", "S-1-5-32-544",
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464" // TrustedInstaller
    };

    /// <summary>
    /// Check whether the current user account has administrative privileges.
    /// Under Windows UAC, an unelevated (asInvoker) process runs with a limited split-token where
    /// BuiltinAdministratorsSid is set to deny-only and filtered out of identity.Groups by .NET.
    /// TokenElevationTypeLimited (3) identifies that the user account is an Administrator that can elevate.
    /// </summary>
    internal static bool IsAdministratorAccount()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            if (identity.Groups?.Contains(adminSid) == true)
                return true;

            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TokenQuery, out var hToken))
            {
                try
                {
                    var pElevationType = Marshal.AllocHGlobal(sizeof(int));
                    try
                    {
                        if (GetTokenInformation(hToken, TokenElevationType, pElevationType, sizeof(int), out _))
                            return Marshal.ReadInt32(pElevationType) == TokenElevationTypeLimited;
                    }
                    finally { Marshal.FreeHGlobal(pElevationType); }
                }
                finally { CloseHandle(hToken); }
            }
        }
        catch { }
        return false;
    }

    internal static bool HasProtectedAcl(FileSystemSecurity security, bool ancestor = false)
    {
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !TrustedOwners.Contains(owner.Value))
            return false;
        var mutable = FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        if (!ancestor) mutable |= FileSystemRights.Write;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                !rule.PropagationFlags.HasFlag(PropagationFlags.InheritOnly) &&
                (rule.FileSystemRights & mutable) != 0 && !TrustedOwners.Contains(rule.IdentityReference.Value)) return false;
        }
        return true;
    }

    public static bool CanRegister(string executable)
    {
        try
        {
            if (!IsAdministratorAccount())
                return false;
            if (string.IsNullOrWhiteSpace(executable))
                return false;
            var file = new FileInfo(executable);
            if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;

            return true;
        }
        catch { return false; }
    }
}
