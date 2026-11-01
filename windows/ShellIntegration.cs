using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PixelTxtConverter
{
    public static class ShellIntegration
    {
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".webp" };
        public static string InstallDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PixelTxtConverter"); } }
        public static string InstalledExe { get { return Path.Combine(InstallDirectory, "PixelTxtConverter.exe"); } }
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PixelTxtConverter";
        private const string Associations = @"Software\Classes\SystemFileAssociations";
        private const string Classes = @"Software\Classes";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string TxpProgId = "RagimGames.PixelTxt.1";

        public static void RegisterTxp(RegistryKey root, string executable)
        {
            using (var extension = root.CreateSubKey(".txp"))
            {
                if (extension.GetValue("PixelTxtOwner") == null)
                    extension.SetValue("PixelTxtPreviousProgId", extension.GetValue("") as string ?? "");
                extension.SetValue("", TxpProgId);
                extension.SetValue("PixelTxtOwner", executable);
            }
            using (var type = root.CreateSubKey(TxpProgId))
            {
                type.SetValue("", "TXP RGB Image");
                type.SetValue("PixelTxtOwner", executable);
                using (var icon = type.CreateSubKey("DefaultIcon")) icon.SetValue("", "\"" + executable + "\",0");
                using (var shell = type.CreateSubKey("shell")) shell.SetValue("", "open");
                using (var open = type.CreateSubKey(@"shell\open")) open.SetValue("", "PNG 이미지로 복원");
                using (var command = type.CreateSubKey(@"shell\open\command")) command.SetValue("", Command(executable, "--to-image"));
            }
        }

        public static bool HasTxp(RegistryKey root, string executable)
        {
            using (var extension = root.OpenSubKey(".txp"))
                if (extension == null || !string.Equals(extension.GetValue("") as string, TxpProgId, StringComparison.Ordinal)) return false;
            using (var command = root.OpenSubKey(TxpProgId + @"\shell\open\command"))
                return command != null && string.Equals(command.GetValue("") as string, Command(executable, "--to-image"), StringComparison.OrdinalIgnoreCase);
        }

        public static void UnregisterTxp(RegistryKey root, string executable)
        {
            using (var extension = root.OpenSubKey(".txp", true))
            {
                if (extension != null && string.Equals(extension.GetValue("PixelTxtOwner") as string, executable, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(extension.GetValue("") as string, TxpProgId, StringComparison.Ordinal))
                    {
                        string previous = extension.GetValue("PixelTxtPreviousProgId") as string;
                        if (string.IsNullOrEmpty(previous)) extension.DeleteValue("", false);
                        else extension.SetValue("", previous);
                    }
                    extension.DeleteValue("PixelTxtPreviousProgId", false);
                    extension.DeleteValue("PixelTxtOwner", false);
                }
            }
            RemoveOwnedVerb(root, TxpProgId, executable);
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static string Command(string executable, string action)
        {
            return "\"" + executable + "\" " + action + " \"%1\"";
        }

        // A root parameter allows tests to use an isolated temporary registry subtree.
        public static void RegisterVerbs(RegistryKey root, string executable)
        {
            foreach (string extension in ImageExtensions)
                RegisterVerb(root, extension, "PixelTxtConverter.ToTxt", "TXT Convert", executable, "--to-txt");
            RegisterVerb(root, ".txt", "PixelTxtConverter.ToImage", "TXT Restore (PNG)", executable, "--to-image");
            RegisterVerb(root, ".txp", "PixelTxtConverter.ToImage", "TXP Restore (PNG)", executable, "--to-image");
        }

        private static void RegisterVerb(RegistryKey root, string extension, string name, string title, string executable, string action)
        {
            using (var key = root.CreateSubKey(extension + @"\shell\" + name))
            {
                key.SetValue("MUIVerb", title);
                key.SetValue("Icon", "\"" + executable + "\",0");
                key.SetValue("MultiSelectModel", "Single");
                key.SetValue("PixelTxtOwner", executable);
                using (var command = key.CreateSubKey("command")) command.SetValue("", Command(executable, action));
            }
        }

        public static void UnregisterVerbs(RegistryKey root, string executable)
        {
            foreach (string extension in ImageExtensions)
                RemoveOwnedVerb(root, extension + @"\shell\PixelTxtConverter.ToTxt", executable);
            RemoveOwnedVerb(root, @".txt\shell\PixelTxtConverter.ToImage", executable);
            RemoveOwnedVerb(root, @".txp\shell\PixelTxtConverter.ToImage", executable);
        }

        private static void RemoveOwnedVerb(RegistryKey root, string path, string executable)
        {
            using (var key = root.OpenSubKey(path))
            {
                if (key == null || !string.Equals(key.GetValue("PixelTxtOwner") as string, executable, StringComparison.OrdinalIgnoreCase)) return;
            }
            root.DeleteSubKeyTree(path, false);
        }

        public static bool IsInstalled()
        {
            if (!File.Exists(InstalledExe)) return false;
            if (AssemblyName.GetAssemblyName(InstalledExe).Version != Assembly.GetExecutingAssembly().GetName().Version) return false;
            using (var root = Registry.CurrentUser.OpenSubKey(Associations)) if (!HasAllVerbs(root, InstalledExe)) return false;
            using (var root = Registry.CurrentUser.OpenSubKey(Classes)) if (root == null || !HasTxp(root, InstalledExe)) return false;
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey))
                return run != null && string.Equals(run.GetValue("PixelTxtConverter") as string, "\"" + InstalledExe + "\" --watch", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasAllVerbs(RegistryKey root, string executable)
        {
            if (root == null) return false;
            foreach (string extension in ImageExtensions)
            {
                using (var key = root.OpenSubKey(extension + @"\shell\PixelTxtConverter.ToTxt\command"))
                    if (key == null || !string.Equals(key.GetValue("") as string, Command(executable, "--to-txt"), StringComparison.OrdinalIgnoreCase)) return false;
            }
            using (var key = root.OpenSubKey(@".txt\shell\PixelTxtConverter.ToImage\command"))
                if (key == null || !string.Equals(key.GetValue("") as string, Command(executable, "--to-image"), StringComparison.OrdinalIgnoreCase)) return false;
            using (var key = root.OpenSubKey(@".txp\shell\PixelTxtConverter.ToImage\command"))
                return key != null && string.Equals(key.GetValue("") as string, Command(executable, "--to-image"), StringComparison.OrdinalIgnoreCase);
        }

        public static void Install()
        {
            WatcherApplication.StopWatcher();
            Directory.CreateDirectory(InstallDirectory);
            string source = Assembly.GetExecutingAssembly().Location;
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase))
                File.Copy(source, InstalledExe, true);
            using (var root = Registry.CurrentUser.CreateSubKey(Associations)) RegisterVerbs(root, InstalledExe);
            using (var root = Registry.CurrentUser.CreateSubKey(Classes)) RegisterTxp(root, InstalledExe);
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey)) run.SetValue("PixelTxtConverter", "\"" + InstalledExe + "\" --watch");
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelTxt.WebpLicense"))
            using (var file = File.Create(Path.Combine(InstallDirectory, "WebP-LICENSE.txt"))) resource.CopyTo(file);
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("PixelTxt.WebpPatents"))
            using (var file = File.Create(Path.Combine(InstallDirectory, "WebP-PATENTS.txt"))) resource.CopyTo(file);
            using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                key.SetValue("DisplayName", "Pixel TXT Converter");
                key.SetValue("DisplayVersion", "2.2.0");
                key.SetValue("Publisher", "Ragim-Games");
                key.SetValue("InstallLocation", InstallDirectory);
                key.SetValue("DisplayIcon", InstalledExe);
                key.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                key.SetValue("NoModify", 1);
                key.SetValue("NoRepair", 1);
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            if (!IsInstalled()) throw new InvalidOperationException("일부 메뉴가 등록되지 않았습니다. 설치 / 메뉴 연결을 다시 실행해 주세요.");
            WatcherApplication.StartWatcher();
        }

        public static void Uninstall()
        {
            WatcherApplication.StopWatcher();
            using (var root = Registry.CurrentUser.OpenSubKey(Associations, true))
                if (root != null) UnregisterVerbs(root, InstalledExe);
            using (var root = Registry.CurrentUser.OpenSubKey(Classes, true)) if (root != null) UnregisterTxp(root, InstalledExe);
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (run != null && string.Equals(run.GetValue("PixelTxtConverter") as string, "\"" + InstalledExe + "\" --watch", StringComparison.OrdinalIgnoreCase)) run.DeleteValue("PixelTxtConverter", false);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            // The running executable cannot delete itself on Windows. Leave only this file;
            // don't launch a delayed shell script or delete unrelated directory contents.
        }
    }
}
