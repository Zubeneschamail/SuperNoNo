using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SuperNoNoInstaller;

internal static class Program
{
    private const string AppName = "SuperNoNo";
    private const string AppExeName = "SuperNoNo.exe";
    private const string SetupExeName = "SuperNoNoSetup.exe";
    private const string PayloadResourceName = "payload.zip";
    private const string Publisher = "Codex";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool silent = args.Any(IsSilentArg);

        try
        {
            if (args.Any(IsUninstallArg))
            {
                return Uninstall(silent);
            }

            return Install(silent);
        }
        catch (Exception ex)
        {
            ShowError(silent, "SuperNoNo 安装失败", ex.Message);
            return 1;
        }
    }

    private static int Install(bool silent)
    {
        string installDir = GetInstallDirectory();
        StopRunningApp(silent);
        ResetInstallDirectory(installDir);
        ExtractPayload(installDir);
        CopyInstallerToInstallDirectory(installDir);
        CreateShortcuts(installDir);
        RegisterUninstaller(installDir);
        LaunchApp(installDir);

        ShowInfo(
            silent,
            "SuperNoNo 安装完成",
            "已安装 SuperNoNo，并创建桌面和开始菜单快捷方式。");

        return 0;
    }

    private static int Uninstall(bool silent)
    {
        if (!silent)
        {
            DialogResult answer = MessageBox.Show(
                "确定要卸载 SuperNoNo 吗？",
                "卸载 SuperNoNo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                return 0;
            }
        }

        string installDir = GetInstallDirectory();
        StopRunningApp(silent);
        DeleteShortcuts();
        RemoveUninstallerRegistration();

        bool runningInsideInstallDir = IsPathInsideDirectory(Environment.ProcessPath, installDir);
        if (Directory.Exists(installDir))
        {
            if (runningInsideInstallDir)
            {
                DeleteDirectoryAfterExit(installDir);
            }
            else
            {
                DeleteDirectorySafely(installDir);
            }
        }

        ShowInfo(silent, "SuperNoNo 已卸载", "SuperNoNo 已从当前用户中移除。");
        return 0;
    }

    private static bool IsSilentArg(string arg)
    {
        return arg.Equals("--silent", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("/silent", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("/s", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUninstallArg(string arg)
    {
        return arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInstallDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppName);
    }

    private static void StopRunningApp(bool silent)
    {
        Process[] runningProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppExeName));
        if (runningProcesses.Length == 0)
        {
            return;
        }

        if (!silent)
        {
            DialogResult answer = MessageBox.Show(
                "检测到 SuperNoNo 正在运行。安装或卸载前需要先关闭它，是否继续？",
                "关闭正在运行的 SuperNoNo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                throw new OperationCanceledException("操作已取消。");
            }
        }

        foreach (Process process in runningProcesses)
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(1500))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void ResetInstallDirectory(string installDir)
    {
        AssertInstallDirectoryIsSafe(installDir);

        if (Directory.Exists(installDir))
        {
            DeleteDirectoryContents(installDir);
        }
        else
        {
            Directory.CreateDirectory(installDir);
        }
    }

    private static void DeleteDirectoryContents(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string childDirectory in Directory.EnumerateDirectories(directory))
        {
            DeleteDirectorySafely(childDirectory);
        }
    }

    private static void DeleteDirectorySafely(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }

    private static void ExtractPayload(string installDir)
    {
        using Stream? payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName);
        if (payload is null)
        {
            throw new InvalidOperationException("安装器缺少内置发布包，请先运行 tools\\build-installer.ps1 重新生成。");
        }

        string fullInstallDir = Path.GetFullPath(installDir);
        using ZipArchive archive = new(payload, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string normalizedEntryName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string destinationPath = Path.GetFullPath(Path.Combine(fullInstallDir, normalizedEntryName));

            if (!IsPathInsideDirectory(destinationPath, fullInstallDir))
            {
                throw new InvalidOperationException("安装包包含非法路径，已停止安装。");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }

        string appExePath = Path.Combine(installDir, AppExeName);
        if (!File.Exists(appExePath))
        {
            throw new FileNotFoundException("发布包中缺少 SuperNoNo.exe。", appExePath);
        }
    }

    private static void CopyInstallerToInstallDirectory(string installDir)
    {
        string? currentInstallerPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentInstallerPath) || !File.Exists(currentInstallerPath))
        {
            return;
        }

        string installedInstallerPath = Path.Combine(installDir, SetupExeName);
        if (string.Equals(
            Path.GetFullPath(currentInstallerPath),
            Path.GetFullPath(installedInstallerPath),
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(currentInstallerPath, installedInstallerPath, overwrite: true);
    }

    private static void CreateShortcuts(string installDir)
    {
        string appExePath = Path.Combine(installDir, AppExeName);
        string setupExePath = Path.Combine(installDir, SetupExeName);
        string desktopShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppName}.lnk");
        string startMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            AppName);

        Directory.CreateDirectory(startMenuDirectory);

        CreateShortcut(
            desktopShortcutPath,
            appExePath,
            installDir,
            appExePath,
            "启动 SuperNoNo");

        CreateShortcut(
            Path.Combine(startMenuDirectory, $"{AppName}.lnk"),
            appExePath,
            installDir,
            appExePath,
            "启动 SuperNoNo");

        CreateShortcut(
            Path.Combine(startMenuDirectory, $"卸载 {AppName}.lnk"),
            setupExePath,
            installDir,
            setupExePath,
            "卸载 SuperNoNo",
            "--uninstall");
    }

    private static void DeleteShortcuts()
    {
        string desktopShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppName}.lnk");
        string startMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            AppName);

        DeleteFileIfExists(desktopShortcutPath);

        if (Directory.Exists(startMenuDirectory))
        {
            DeleteDirectorySafely(startMenuDirectory);
        }
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string iconPath,
        string description,
        string? arguments = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        object shellLinkObject = new ShellLink();
        try
        {
            IShellLinkW shellLink = (IShellLinkW)shellLinkObject;
            shellLink.SetPath(targetPath);
            shellLink.SetWorkingDirectory(workingDirectory);
            shellLink.SetDescription(description);
            shellLink.SetIconLocation(iconPath, 0);

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                shellLink.SetArguments(arguments);
            }

            IPersistFile persistFile = (IPersistFile)shellLinkObject;
            persistFile.Save(shortcutPath, remember: true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLinkObject);
        }
    }

    private static void RegisterUninstaller(string installDir)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}");

        if (key is null)
        {
            return;
        }

        string appExePath = Path.Combine(installDir, AppExeName);
        string setupExePath = Path.Combine(installDir, SetupExeName);
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        key.SetValue("DisplayName", AppName, RegistryValueKind.String);
        key.SetValue("DisplayIcon", appExePath, RegistryValueKind.String);
        key.SetValue("DisplayVersion", version, RegistryValueKind.String);
        key.SetValue("EstimatedSize", CalculateDirectorySizeKb(installDir), RegistryValueKind.DWord);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("Publisher", Publisher, RegistryValueKind.String);
        key.SetValue("UninstallString", $"\"{setupExePath}\" --uninstall", RegistryValueKind.String);
    }

    private static void RemoveUninstallerRegistration()
    {
        Registry.CurrentUser.DeleteSubKeyTree(
            $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}",
            throwOnMissingSubKey: false);
    }

    private static int CalculateDirectorySizeKb(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        long totalBytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);

        return (int)Math.Max(1, totalBytes / 1024);
    }

    private static void LaunchApp(string installDir)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(installDir, AppExeName),
            WorkingDirectory = installDir,
            UseShellExecute = true
        });
    }

    private static void DeleteDirectoryAfterExit(string directory)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{directory}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    private static void AssertInstallDirectoryIsSafe(string installDir)
    {
        string fullInstallDir = Path.GetFullPath(installDir);
        string expectedRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs"));

        if (!IsPathInsideDirectory(fullInstallDir, expectedRoot)
            || !string.Equals(Path.GetFileName(fullInstallDir), AppName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("安装目录不在预期的用户程序目录中，已停止安装。");
        }
    }

    private static bool IsPathInsideDirectory(string? path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void ShowInfo(bool silent, string title, string message)
    {
        if (!silent)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static void ShowError(bool silent, string title, string message)
    {
        if (!silent)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
