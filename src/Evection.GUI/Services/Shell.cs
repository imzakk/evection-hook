using System.Diagnostics;

namespace Evection.GUI.Services;

/// <summary>Opening folders and links in the operating system.</summary>
public static class Shell
{
    public static void Open(string pathOrUrl)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", pathOrUrl);
            else
                Process.Start("xdg-open", pathOrUrl);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nothing sensible to do if the OS can't open it.
        }
    }

}
