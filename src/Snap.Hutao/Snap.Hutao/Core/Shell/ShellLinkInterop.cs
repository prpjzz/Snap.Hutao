// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Win32.System.Com;
using Snap.Hutao.Win32.UI.Shell;
using Snap.Hutao.Win32.UI.WindowsAndMessaging;
using System.IO;
using WinRT;
using static Snap.Hutao.Win32.Macros;
using static Snap.Hutao.Win32.Ole32;

namespace Snap.Hutao.Core.Shell;

[Injection(InjectAs.Transient, typeof(IShellLinkInterop))]
internal sealed class ShellLinkInterop : IShellLinkInterop
{
    public bool TryCreateDesktopShortcutForElevatedLaunch()
    {
        string targetLogoPath = HutaoRuntime.GetDataFolderFile("ShellLinkLogo.ico");
        string elevatedLauncherPath = HutaoRuntime.GetDataFolderFile("Snap.Hutao.Elevated.Launcher.exe");

        try
        {
            InstalledLocation.CopyFileFromApplicationUri("ms-appx:///Assets/Logo.ico", targetLogoPath);
            InstalledLocation.CopyFileFromApplicationUri("ms-appx:///Snap.Hutao.Elevated.Launcher.exe", elevatedLauncherPath);
        }
        catch
        {
            return false;
        }

        return UnsafeTryCreateDesktopShortcutForElevatedLaunch(targetLogoPath, elevatedLauncherPath);
    }

    private static bool UnsafeTryCreateDesktopShortcutForElevatedLaunch(string targetLogoPath, string elevatedLauncherPath)
    {
        if (!SUCCEEDED(CoCreateInstance(in ShellLink.CLSID, default, CLSCTX.CLSCTX_INPROC_SERVER, in IShellLinkW.IID, out ObjectReference<IShellLinkW.Vftbl> shellLink)))
        {
            return false;
        }

        using (shellLink)
        {
            shellLink.SetPath(elevatedLauncherPath);
            shellLink.SetArguments(HutaoRuntime.FamilyName);
            shellLink.SetShowCmd(SHOW_WINDOW_CMD.SW_NORMAL);
            shellLink.SetIconLocation(targetLogoPath, 0);

            if (!SUCCEEDED(shellLink.TryAs(IPersistFile.IID, out ObjectReference<IPersistFile.Vftbl> persistFile)))
            {
                persistFile?.Dispose();
                return false;
            }

            using (persistFile)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string target = Path.Combine(desktop, $"{SH.FormatAppNameAndVersion(HutaoRuntime.Version)}.lnk");

                return SUCCEEDED(persistFile.Save(target, false));
            }
        }
    }
}