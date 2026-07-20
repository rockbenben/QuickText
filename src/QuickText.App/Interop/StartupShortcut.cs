using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace QuickText.App.Interop;

/// <summary>
/// Creates/removes a "QuickText.lnk" in the user's Startup folder via the Windows shell
/// (IShellLink). Portable mode uses this instead of the HKCU Run registry key so a USB copy
/// starts with Windows without writing to the registry. STA-only — call from the UI thread.
/// </summary>
internal static class StartupShortcut
{
    private const string LinkName = "QuickText.lnk";

    private static string LinkPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), LinkName);

    public static bool Exists() => System.IO.File.Exists(LinkPath);

    public static void Create()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(exe);
            link.SetArguments(Autostart.Flag);   // marks the login launch as silent (no search panel)
            link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(exe) ?? "");
            link.SetDescription("QuickText");
            ((IPersistFile)link).Save(LinkPath, false);
        }
        catch { /* creating the shortcut must never crash the settings save */ }
        finally { Marshal.ReleaseComObject(link); }
    }

    public static void Delete()
    {
        try { if (Exists()) System.IO.File.Delete(LinkPath); } catch { }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
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
}
