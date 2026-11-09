using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace QuickText.App.Interop;

internal static class ElevationPolicy
{
    private static readonly HashSet<string> TrustedOwners = new(StringComparer.Ordinal)
    {
        "S-1-5-18", "S-1-5-32-544",
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464" // TrustedInstaller
    };

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
            using var identity = WindowsIdentity.GetCurrent();
            if (identity.Groups?.Contains(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)) != true)
                return false;
            var file = new FileInfo(executable);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!file.FullName.StartsWith(Path.TrimEndingDirectorySeparator(programFiles) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) || !file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                !HasProtectedAcl(file.GetAccessControl())) return false;
            bool ancestor = false;
            for (var folder = file.Directory; folder != null; folder = folder.Parent)
            {
                if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint) || !HasProtectedAcl(folder.GetAccessControl(), ancestor)) return false;
                ancestor = true;
            }
            return true;
        }
        catch { return false; }
    }
}
